using SuperSwitch.Common.Logging;
using SuperSwitch.Graphics.GAL;
using Silk.NET.Vulkan;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SuperSwitch.Graphics.Vulkan
{
    class ShaderCollection : IProgram
    {
        private readonly PipelineShaderStageCreateInfo[] _infos;
        private readonly Shader[] _shaders;

        private readonly PipelineLayoutCacheEntry _plce;

        public PipelineLayout PipelineLayout => _plce.PipelineLayout;

        public bool HasMinimalLayout { get; }
        public bool UsePushDescriptors { get; }
        public bool IsCompute { get; }
        public bool HasTessellationControlShader => (Stages & (1u << 3)) != 0;

        public bool UpdateTexturesWithoutTemplate { get; }

        public uint Stages { get; }

        public PipelineStageFlags IncoherentBufferWriteStages { get; }
        public PipelineStageFlags IncoherentTextureWriteStages { get; }

        public ResourceBindingSegment[][] ClearSegments { get; }
        public ResourceBindingSegment[][] BindingSegments { get; }
        public DescriptorSetTemplate[] Templates { get; }

        public ProgramLinkStatus LinkStatus { get; private set; }

        public readonly SpecDescription[] SpecDescriptions;

        public bool IsLinked
        {
            get
            {
                if (LinkStatus == ProgramLinkStatus.Incomplete)
                {
                    ProgramLinkStatus status = CheckProgramLink(true);

                    // Background compilation may have already flagged a failure; prefer that decision.
                    if (LinkStatus == ProgramLinkStatus.Incomplete)
                    {
                        LinkStatus = status;
                    }
                }

                return LinkStatus == ProgramLinkStatus.Success;
            }
        }

        private HashTableSlim<PipelineUid, Auto<DisposablePipeline>> _graphicsPipelineCache;
        private HashTableSlim<SpecData, Auto<DisposablePipeline>> _computePipelineCache;

        private readonly VulkanRenderer _gd;
        private Device _device;
        private bool _initialized;

        private ProgramPipelineState _state;
        private DisposableRenderPass _dummyRenderPass;
        private readonly Task _compileTask;
        private bool _firstBackgroundUse;

        /// <summary>
        /// True for programs translated from guest (game) shaders. Only these may have draws skipped
        /// while their pipelines compile; internal helper programs must always be waited on.
        /// </summary>
        public bool IsGuestProgram { get; }

        /// <summary>
        /// A graphics pipeline variant being compiled on a worker thread (SuperSwitch async shader compilation).
        /// </summary>
        private sealed class PendingGraphicsPipeline
        {
            public PipelineUid Key;
            public PipelineState State;
            public Auto<DisposableRenderPass> RenderPass;
            public Task<(Result Result, Pipeline Handle)> Task;
            public bool Harvested;
        }

        private HashTableSlim<PipelineUid, PendingGraphicsPipeline> _pendingGraphicsPipelines;
        private List<PendingGraphicsPipeline> _pendingGraphicsPipelineList;

        public ShaderCollection(
            VulkanRenderer gd,
            Device device,
            ShaderSource[] shaders,
            ResourceLayout resourceLayout,
            SpecDescription[] specDescription = null,
            bool isMinimal = false,
            bool isGuest = false)
        {
            IsGuestProgram = isGuest;
            _gd = gd;
            _device = device;

            if (specDescription != null && specDescription.Length != shaders.Length)
            {
                throw new ArgumentException($"{nameof(specDescription)} array length must match {nameof(shaders)} array if provided");
            }

            gd.Shaders.Add(this);

            Shader[] internalShaders = new Shader[shaders.Length];

            _infos = new PipelineShaderStageCreateInfo[shaders.Length];

            SpecDescriptions = specDescription;

            LinkStatus = ProgramLinkStatus.Incomplete;

            uint stages = 0;

            for (int i = 0; i < shaders.Length; i++)
            {
                Shader shader = new(gd.Api, device, shaders[i]);

                stages |= 1u << shader.StageFlags switch
                {
                    ShaderStageFlags.FragmentBit => 1,
                    ShaderStageFlags.GeometryBit => 2,
                    ShaderStageFlags.TessellationControlBit => 3,
                    ShaderStageFlags.TessellationEvaluationBit => 4,
                    _ => 0,
                };

                if (shader.StageFlags == ShaderStageFlags.ComputeBit)
                {
                    IsCompute = true;
                }

                internalShaders[i] = shader;
            }

            _shaders = internalShaders;

            bool usePushDescriptors = !isMinimal &&
                VulkanConfiguration.UsePushDescriptors &&
                _gd.Capabilities.SupportsPushDescriptors &&
                !IsCompute &&
                !HasPushDescriptorsBug(gd) &&
                CanUsePushDescriptors(gd, resourceLayout, IsCompute);

            ReadOnlyCollection<ResourceDescriptorCollection> sets = usePushDescriptors ?
                BuildPushDescriptorSets(gd, resourceLayout.Sets) : resourceLayout.Sets;

            _plce = gd.PipelineLayoutCache.GetOrCreate(gd, device, sets, usePushDescriptors);

            HasMinimalLayout = isMinimal;
            UsePushDescriptors = usePushDescriptors;

            Stages = stages;

            ClearSegments = BuildClearSegments(sets);
            BindingSegments = BuildBindingSegments(resourceLayout.SetUsages, out bool usesBufferTextures);
            Templates = BuildTemplates(usePushDescriptors);
            (IncoherentBufferWriteStages, IncoherentTextureWriteStages) = BuildIncoherentStages(resourceLayout.SetUsages);

            // Updating buffer texture bindings using template updates crashes the Adreno driver on Windows.
            UpdateTexturesWithoutTemplate = gd.IsQualcommProprietary && usesBufferTextures;

            _compileTask = Task.CompletedTask;
            _firstBackgroundUse = false;
        }

        public ShaderCollection(
            VulkanRenderer gd,
            Device device,
            ShaderSource[] sources,
            ResourceLayout resourceLayout,
            ProgramPipelineState state,
            bool fromCache) : this(gd, device, sources, resourceLayout)
        {
            _state = state;

            _compileTask = BackgroundCompilation();
            _firstBackgroundUse = !fromCache;

            IsGuestProgram = true;
        }

        private static bool HasPushDescriptorsBug(VulkanRenderer gd)
        {
            // Those GPUs/drivers do not work properly with push descriptors, so we must force disable them.
            return gd.IsNvidiaPreTuring || (gd.IsIntelArc && (gd.IsIntelWindows || gd.IsIntelLinux));
        }

        private static bool CanUsePushDescriptors(VulkanRenderer gd, ResourceLayout layout, bool isCompute)
        {
            // If binding 3 is immediately used, use an alternate set of reserved bindings.
            ReadOnlyCollection<ResourceUsage> uniformUsage = layout.SetUsages[0].Usages;
            bool hasBinding3 = uniformUsage.Any(x => x.Binding == 3);
            int[] reserved = isCompute ? [] : gd.GetPushDescriptorReservedBindings(hasBinding3);

            // Can't use any of the reserved usages.
            for (int i = 0; i < uniformUsage.Count; i++)
            {
                int binding = uniformUsage[i].Binding;

                if (reserved.Contains(binding) ||
                    binding >= Constants.MaxPushDescriptorBinding ||
                    binding >= gd.Capabilities.MaxPushDescriptors + reserved.Count(id => id < binding))
                {
                    return false;
                }
            }

            //Prevent the sum of descriptors from exceeding MaxPushDescriptors
            int totalDescriptors = 0;
            foreach (ResourceDescriptor desc in layout.Sets.First().Descriptors)
            {
                if (!reserved.Contains(desc.Binding))
                    totalDescriptors += desc.Count;
            }

            if (totalDescriptors > gd.Capabilities.MaxPushDescriptors)
                return false;

            return true;
        }

        private static ReadOnlyCollection<ResourceDescriptorCollection> BuildPushDescriptorSets(
            VulkanRenderer gd,
            ReadOnlyCollection<ResourceDescriptorCollection> sets)
        {
            // The reserved bindings were selected when determining if push descriptors could be used.
            int[] reserved = gd.GetPushDescriptorReservedBindings(false);

            ResourceDescriptorCollection[] result = new ResourceDescriptorCollection[sets.Count];

            for (int i = 0; i < sets.Count; i++)
            {
                if (i == 0)
                {
                    // Push descriptors apply here. Remove reserved bindings.
                    ResourceDescriptorCollection original = sets[i];

                    ResourceDescriptor[] pdUniforms = new ResourceDescriptor[original.Descriptors.Count];
                    int j = 0;

                    foreach (ResourceDescriptor descriptor in original.Descriptors)
                    {
                        if (reserved.Contains(descriptor.Binding))
                        {
                            // If the binding is reserved, set its descriptor count to 0.
                            pdUniforms[j++] = new ResourceDescriptor(
                                descriptor.Binding,
                                0,
                                descriptor.Type,
                                descriptor.Stages);
                        }
                        else
                        {
                            pdUniforms[j++] = descriptor;
                        }
                    }

                    result[i] = new ResourceDescriptorCollection(new(pdUniforms));
                }
                else
                {
                    result[i] = sets[i];
                }
            }

            return new(result);
        }

        private static ResourceBindingSegment[][] BuildClearSegments(ReadOnlyCollection<ResourceDescriptorCollection> sets)
        {
            ResourceBindingSegment[][] segments = new ResourceBindingSegment[sets.Count][];

            for (int setIndex = 0; setIndex < sets.Count; setIndex++)
            {
                List<ResourceBindingSegment> currentSegments = [];

                ResourceDescriptor currentDescriptor = default;
                int currentCount = 0;

                for (int index = 0; index < sets[setIndex].Descriptors.Count; index++)
                {
                    ResourceDescriptor descriptor = sets[setIndex].Descriptors[index];

                    if (currentDescriptor.Binding + currentCount != descriptor.Binding ||
                        currentDescriptor.Type != descriptor.Type ||
                        currentDescriptor.Stages != descriptor.Stages ||
                        currentDescriptor.Count > 1 ||
                        descriptor.Count > 1)
                    {
                        if (currentCount != 0)
                        {
                            currentSegments.Add(new ResourceBindingSegment(
                                currentDescriptor.Binding,
                                currentCount,
                                currentDescriptor.Type,
                                currentDescriptor.Stages,
                                currentDescriptor.Count > 1));
                        }

                        currentDescriptor = descriptor;
                        currentCount = descriptor.Count;
                    }
                    else
                    {
                        currentCount += descriptor.Count;
                    }
                }

                if (currentCount != 0)
                {
                    currentSegments.Add(new ResourceBindingSegment(
                        currentDescriptor.Binding,
                        currentCount,
                        currentDescriptor.Type,
                        currentDescriptor.Stages,
                        currentDescriptor.Count > 1));
                }

                segments[setIndex] = currentSegments.ToArray();
            }

            return segments;
        }

        private static ResourceBindingSegment[][] BuildBindingSegments(ReadOnlyCollection<ResourceUsageCollection> setUsages, out bool usesBufferTextures)
        {
            usesBufferTextures = false;

            ResourceBindingSegment[][] segments = new ResourceBindingSegment[setUsages.Count][];

            for (int setIndex = 0; setIndex < setUsages.Count; setIndex++)
            {
                List<ResourceBindingSegment> currentSegments = [];

                ResourceUsage currentUsage = default;
                int currentCount = 0;

                for (int index = 0; index < setUsages[setIndex].Usages.Count; index++)
                {
                    ResourceUsage usage = setUsages[setIndex].Usages[index];

                    if (usage.Type == ResourceType.BufferTexture)
                    {
                        usesBufferTextures = true;
                    }

                    if (currentUsage.Binding + currentCount != usage.Binding ||
                        currentUsage.Type != usage.Type ||
                        currentUsage.Stages != usage.Stages ||
                        currentUsage.ArrayLength > 1 ||
                        usage.ArrayLength > 1)
                    {
                        if (currentCount != 0)
                        {
                            currentSegments.Add(new ResourceBindingSegment(
                                currentUsage.Binding,
                                currentCount,
                                currentUsage.Type,
                                currentUsage.Stages,
                                currentUsage.ArrayLength > 1));
                        }

                        currentUsage = usage;
                        currentCount = usage.ArrayLength;
                    }
                    else
                    {
                        currentCount++;
                    }
                }

                if (currentCount != 0)
                {
                    currentSegments.Add(new ResourceBindingSegment(
                        currentUsage.Binding,
                        currentCount,
                        currentUsage.Type,
                        currentUsage.Stages,
                        currentUsage.ArrayLength > 1));
                }

                segments[setIndex] = currentSegments.ToArray();
            }

            return segments;
        }

        private DescriptorSetTemplate[] BuildTemplates(bool usePushDescriptors)
        {
            DescriptorSetTemplate[] templates = new DescriptorSetTemplate[BindingSegments.Length];

            for (int setIndex = 0; setIndex < BindingSegments.Length; setIndex++)
            {
                if (usePushDescriptors && setIndex == 0)
                {
                    // Push descriptors get updated using templates owned by the pipeline layout.
                    continue;
                }

                ResourceBindingSegment[] segments = BindingSegments[setIndex];

                if (segments != null && segments.Length > 0)
                {
                    templates[setIndex] = new DescriptorSetTemplate(
                        _gd,
                        _device,
                        segments,
                        _plce,
                        IsCompute ? PipelineBindPoint.Compute : PipelineBindPoint.Graphics,
                        setIndex);
                }
            }

            return templates;
        }

        private static PipelineStageFlags GetPipelineStages(ResourceStages stages)
        {
            PipelineStageFlags result = 0;

            if ((stages & ResourceStages.Compute) != 0)
            {
                result |= PipelineStageFlags.ComputeShaderBit;
            }

            if ((stages & ResourceStages.Vertex) != 0)
            {
                result |= PipelineStageFlags.VertexShaderBit;
            }

            if ((stages & ResourceStages.Fragment) != 0)
            {
                result |= PipelineStageFlags.FragmentShaderBit;
            }

            if ((stages & ResourceStages.Geometry) != 0)
            {
                result |= PipelineStageFlags.GeometryShaderBit;
            }

            if ((stages & ResourceStages.TessellationControl) != 0)
            {
                result |= PipelineStageFlags.TessellationControlShaderBit;
            }

            if ((stages & ResourceStages.TessellationEvaluation) != 0)
            {
                result |= PipelineStageFlags.TessellationEvaluationShaderBit;
            }

            return result;
        }

        private static (PipelineStageFlags Buffer, PipelineStageFlags Texture) BuildIncoherentStages(ReadOnlyCollection<ResourceUsageCollection> setUsages)
        {
            PipelineStageFlags buffer = PipelineStageFlags.None;
            PipelineStageFlags texture = PipelineStageFlags.None;

            foreach (ResourceUsageCollection set in setUsages)
            {
                foreach (ResourceUsage range in set.Usages)
                {
                    if (range.Write)
                    {
                        PipelineStageFlags stages = GetPipelineStages(range.Stages);

                        switch (range.Type)
                        {
                            case ResourceType.Image:
                                texture |= stages;
                                break;
                            case ResourceType.StorageBuffer:
                            case ResourceType.BufferImage:
                                buffer |= stages;
                                break;
                        }
                    }
                }
            }

            return (buffer, texture);
        }

        private async Task BackgroundCompilation()
        {
            await Task.WhenAll(_shaders.Select(shader => shader.CompileTask));

            if (Array.Exists(_shaders, shader => shader.CompileStatus == ProgramLinkStatus.Failure))
            {
                LinkStatus = ProgramLinkStatus.Failure;

                return;
            }

            try
            {
                if (IsCompute)
                {
                    CreateBackgroundComputePipeline();
                }
                else
                {
                    CreateBackgroundGraphicsPipeline();
                }
            }
            catch (VulkanException e)
            {
                Logger.Error?.PrintMsg(LogClass.Gpu, $"Background Compilation failed: {e.Message}");

                LinkStatus = ProgramLinkStatus.Failure;
            }
        }

        private void EnsureShadersReady()
        {
            if (!_initialized)
            {
                // SuperSwitch: only wait for the shader modules here, not for the background pipeline compile.
                // Binding a program used to block until its first pipeline was fully compiled by the driver,
                // which is the main source of shader stutter. The pipeline wait now happens at draw time
                // (see IsLinked / PipelineBase.CreatePipeline), where async mode can skip the draw instead.
                bool failed = false;

                for (int i = 0; i < _shaders.Length; i++)
                {
                    Shader shader = _shaders[i];

                    shader.WaitForCompile();

                    if (shader.CompileStatus != ProgramLinkStatus.Success)
                    {
                        failed = true;
                    }

                    _infos[i] = shader.GetInfo();
                }

                if (failed)
                {
                    LinkStatus = ProgramLinkStatus.Failure;
                }

                _initialized = true;
            }
        }

        public PipelineShaderStageCreateInfo[] GetInfos()
        {
            EnsureShadersReady();

            return _infos;
        }

        protected DisposableRenderPass CreateDummyRenderPass()
        {
            if (_dummyRenderPass.Value.Handle != 0)
            {
                return _dummyRenderPass;
            }

            return _dummyRenderPass = _state.ToRenderPass(_gd, _device);
        }

        public void CreateBackgroundComputePipeline()
        {
            PipelineState pipeline = new();
            pipeline.Initialize();

            pipeline.Stages[0] = _shaders[0].GetInfo();
            pipeline.StagesCount = 1;
            pipeline.PipelineLayout = PipelineLayout;

            pipeline.CreateComputePipeline(_gd, _device, this, (_gd.Pipeline as PipelineBase).PipelineCache);
            pipeline.Dispose();
        }

        public void CreateBackgroundGraphicsPipeline()
        {
            // To compile shaders in the background in Vulkan, we need to create valid pipelines using the shader modules.
            // The GPU provides pipeline state via the GAL that can be converted into our internal Vulkan pipeline state.
            // This should match the pipeline state at the time of the first draw. If it doesn't, then it'll likely be
            // close enough that the GPU driver will reuse the compiled shader for the different state.

            // First, we need to create a render pass object compatible with the one that will be used at runtime.
            // The active attachment formats have been provided by the abstraction layer.
            DisposableRenderPass renderPass = CreateDummyRenderPass();

            PipelineState pipeline = _state.ToVulkanPipelineState(_gd);

            // Copy the shader stage info to the pipeline.
            Span<PipelineShaderStageCreateInfo> stages = pipeline.Stages.AsSpan();

            for (int i = 0; i < _shaders.Length; i++)
            {
                stages[i] = _shaders[i].GetInfo();
            }

            pipeline.HasTessellationControlShader = HasTessellationControlShader;
            pipeline.StagesCount = (uint)_shaders.Length;
            pipeline.PipelineLayout = PipelineLayout;

            pipeline.CreateGraphicsPipeline(_gd, _device, this, (_gd.Pipeline as PipelineBase).PipelineCache, renderPass.Value, throwOnError: true);
            pipeline.Dispose();
        }

        public ProgramLinkStatus CheckProgramLink(bool blocking)
        {
            if (LinkStatus == ProgramLinkStatus.Incomplete)
            {
                ProgramLinkStatus resultStatus = ProgramLinkStatus.Success;

                foreach (Shader shader in _shaders)
                {
                    if (shader.CompileStatus == ProgramLinkStatus.Incomplete)
                    {
                        if (blocking)
                        {
                            // Wait for this shader to finish compiling.
                            shader.WaitForCompile();

                            if (shader.CompileStatus != ProgramLinkStatus.Success)
                            {
                                resultStatus = ProgramLinkStatus.Failure;
                            }
                        }
                        else
                        {
                            return ProgramLinkStatus.Incomplete;
                        }
                    }
                }

                if (_compileTask.IsFaulted)
                {
                    return ProgramLinkStatus.Failure;
                }

                if (!_compileTask.IsCompleted)
                {
                    if (blocking)
                    {
                        _compileTask.Wait();

                        if (LinkStatus == ProgramLinkStatus.Failure)
                        {
                            return ProgramLinkStatus.Failure;
                        }
                    }
                    else
                    {
                        return ProgramLinkStatus.Incomplete;
                    }
                }

                return resultStatus;
            }

            return LinkStatus;
        }

        public byte[] GetBinary()
        {
            return null;
        }

        public DescriptorSetTemplate GetPushDescriptorTemplate(long updateMask)
        {
            return _plce.GetPushDescriptorTemplate(IsCompute ? PipelineBindPoint.Compute : PipelineBindPoint.Graphics, updateMask);
        }

        public void AddComputePipeline(ref SpecData key, Auto<DisposablePipeline> pipeline)
        {
            (_computePipelineCache ??= new()).Add(ref key, pipeline);
        }

        public void AddGraphicsPipeline(ref PipelineUid key, Auto<DisposablePipeline> pipeline)
        {
            (_graphicsPipelineCache ??= new()).Add(ref key, pipeline);
        }

        public bool TryGetComputePipeline(ref SpecData key, out Auto<DisposablePipeline> pipeline)
        {
            if (_computePipelineCache == null)
            {
                pipeline = default;
                return false;
            }

            if (_computePipelineCache.TryGetValue(ref key, out pipeline))
            {
                return true;
            }

            return false;
        }

        public bool TryGetGraphicsPipeline(ref PipelineUid key, out Auto<DisposablePipeline> pipeline)
        {
            if (_graphicsPipelineCache == null)
            {
                pipeline = default;
                return false;
            }

            if (!_graphicsPipelineCache.TryGetValue(ref key, out pipeline))
            {
                if (_firstBackgroundUse)
                {
                    Logger.Warning?.Print(LogClass.Gpu, "Background pipeline compile missed on draw - incorrect pipeline state?");
                    _firstBackgroundUse = false;
                }

                return false;
            }

            _firstBackgroundUse = false;

            return true;
        }

        /// <summary>
        /// Gets the graphics pipeline for the given state, compiling it on a worker thread if it doesn't exist yet.
        /// Must only be called from the render thread, and only after the program has finished linking.
        /// </summary>
        /// <param name="gd">Renderer</param>
        /// <param name="state">Current pipeline state</param>
        /// <param name="cache">Vulkan pipeline cache</param>
        /// <param name="renderPass">Render pass compatible with the current framebuffer</param>
        /// <param name="waitBudgetTicks">How long to wait for a freshly queued compile before giving up for this draw, in Stopwatch ticks</param>
        /// <param name="pipeline">The pipeline, or null if the state is invalid and the draw must be skipped</param>
        /// <returns>True if a result is available now, false if the pipeline is still compiling</returns>
        public bool TryGetOrQueueGraphicsPipeline(
            VulkanRenderer gd,
            ref PipelineState state,
            PipelineCache cache,
            Auto<DisposableRenderPass> renderPass,
            long waitBudgetTicks,
            out Auto<DisposablePipeline> pipeline)
        {
            // Move any finished background compiles into the pipeline cache, so they don't hold resources
            // (render passes, native state copies) until the same state happens to be drawn again.
            HarvestCompletedPipelines(gd);

            if (TryGetGraphicsPipeline(ref state.Internal, out pipeline))
            {
                return true;
            }

            if (state.IsInvalidGraphicsState)
            {
                AddGraphicsPipeline(ref state.Internal, null);
                pipeline = null;

                return true;
            }

            _pendingGraphicsPipelines ??= new();
            _pendingGraphicsPipelineList ??= new();

            if (_pendingGraphicsPipelines.TryGetValue(ref state.Internal, out _))
            {
                // Already compiling (finished ones were harvested above). Skip without waiting again,
                // so a frame full of draws using the same new variant doesn't pay the wait budget per draw.
                pipeline = null;

                return false;
            }

            PendingGraphicsPipeline pending = new()
            {
                // The key is a separate copy: the worker's state copy may be temporarily modified during creation.
                Key = state.Internal,
                State = state.CloneForBackgroundCompile(),
                RenderPass = renderPass,
            };

            renderPass.IncrementReferenceCount();

            _pendingGraphicsPipelines.Add(ref pending.Key, pending);
            _pendingGraphicsPipelineList.Add(pending);

            Device device = _device;

            // On the dedicated low-priority compile threads, not the shared thread pool (see PipelineCompileWorkers).
            pending.Task = PipelineCompileWorkers.Run(() =>
            {
                try
                {
                    Result result = pending.State.CreateGraphicsPipelineHandle(gd, device, cache, pending.RenderPass.GetUnsafe().Value, out Pipeline handle);

                    return (result, handle);
                }
                catch (Exception e)
                {
                    Logger.Error?.PrintMsg(LogClass.Gpu, $"Async pipeline compilation failed: {e.Message}");

                    return (Result.ErrorInitializationFailed, default(Pipeline));
                }
            });

            if (waitBudgetTicks > 0)
            {
                // Many variants compile in well under a millisecond (the driver reuses the compiled shaders),
                // so wait briefly before skipping the draw to avoid needless pop-in. Only done once per variant.
                long deadline = Stopwatch.GetTimestamp() + waitBudgetTicks;

                SpinWait spin = new();

                while (!pending.Task.IsCompleted && Stopwatch.GetTimestamp() < deadline)
                {
                    spin.SpinOnce(-1);
                }
            }

            if (!pending.Task.IsCompleted)
            {
                pipeline = null;

                return false;
            }

            pipeline = HarvestPendingPipeline(gd, pending);
            _pendingGraphicsPipelineList.Remove(pending);

            return true;
        }

        private void HarvestCompletedPipelines(VulkanRenderer gd)
        {
            if (_pendingGraphicsPipelineList == null || _pendingGraphicsPipelineList.Count == 0)
            {
                return;
            }

            for (int i = _pendingGraphicsPipelineList.Count - 1; i >= 0; i--)
            {
                PendingGraphicsPipeline pending = _pendingGraphicsPipelineList[i];

                if (pending.Task.IsCompleted)
                {
                    HarvestPendingPipeline(gd, pending);
                    _pendingGraphicsPipelineList.RemoveAt(i);
                }
            }
        }

        private Auto<DisposablePipeline> HarvestPendingPipeline(VulkanRenderer gd, PendingGraphicsPipeline pending)
        {
            (Result result, Pipeline handle) = pending.Task.Result;

            Auto<DisposablePipeline> pipeline = null;

            if (!result.IsError)
            {
                pipeline = new Auto<DisposablePipeline>(new DisposablePipeline(gd.Api, _device, handle));
            }

            _pendingGraphicsPipelines.Remove(ref pending.Key);

            if (TryGetGraphicsPipeline(ref pending.Key, out Auto<DisposablePipeline> existing))
            {
                // The synchronous path already created this variant meanwhile; keep that one.
                pipeline?.Dispose();
                pipeline = existing;
            }
            else
            {
                AddGraphicsPipeline(ref pending.Key, pipeline);
            }

            ReleasePending(pending);

            return pipeline;
        }

        private static void ReleasePending(PendingGraphicsPipeline pending)
        {
            if (!pending.Harvested)
            {
                pending.Harvested = true;
                pending.State.Dispose();
                pending.RenderPass.DecrementReferenceCount();
            }
        }

        /// <summary>
        /// Blocks until all background compilation for this program has finished.
        /// Must be called before the pipeline cache or pipeline layouts it uses are destroyed.
        /// </summary>
        internal void WaitForBackgroundWork()
        {
            try
            {
                _compileTask.Wait();
            }
            catch (AggregateException)
            {
                // Failures are already reflected in the link status.
            }

            if (_pendingGraphicsPipelineList != null)
            {
                foreach (PendingGraphicsPipeline pending in _pendingGraphicsPipelineList)
                {
                    pending.Task.Wait();
                }
            }
        }

        public void UpdateDescriptorCacheCommandBufferIndex(int commandBufferIndex)
        {
            _plce.UpdateCommandBufferIndex(commandBufferIndex);
        }

        public Auto<DescriptorSetCollection> GetNewDescriptorSetCollection(int setIndex, out bool isNew)
        {
            return _plce.GetNewDescriptorSetCollection(setIndex, out isNew);
        }

        public Auto<DescriptorSetCollection> GetNewManualDescriptorSetCollection(CommandBufferScoped cbs, int setIndex, out int cacheIndex)
        {
            return _plce.GetNewManualDescriptorSetCollection(cbs, setIndex, out cacheIndex);
        }

        public void UpdateManualDescriptorSetCollectionOwnership(CommandBufferScoped cbs, int setIndex, int cacheIndex)
        {
            _plce.UpdateManualDescriptorSetCollectionOwnership(cbs, setIndex, cacheIndex);
        }

        public void ReleaseManualDescriptorSetCollection(int setIndex, int cacheIndex)
        {
            _plce.ReleaseManualDescriptorSetCollection(setIndex, cacheIndex);
        }

        public bool HasSameLayout(ShaderCollection other)
        {
            return other != null && _plce == other._plce;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!_gd.Shaders.Remove(this))
                {
                    return;
                }

                if (_pendingGraphicsPipelineList != null)
                {
                    // Worker threads may still be using our shader modules and layout; wait for them before destroying anything.
                    foreach (PendingGraphicsPipeline pending in _pendingGraphicsPipelineList)
                    {
                        (Result result, Pipeline handle) = pending.Task.Result;

                        if (!result.IsError && handle.Handle != 0)
                        {
                            unsafe
                            {
                                _gd.Api.DestroyPipeline(_device, handle, null);
                            }
                        }

                        ReleasePending(pending);
                    }

                    _pendingGraphicsPipelineList.Clear();
                }

                for (int i = 0; i < _shaders.Length; i++)
                {
                    _shaders[i].Dispose();
                }

                if (_graphicsPipelineCache != null)
                {
                    foreach (Auto<DisposablePipeline> pipeline in _graphicsPipelineCache.Values)
                    {
                        pipeline?.Dispose();
                    }
                }

                if (_computePipelineCache != null)
                {
                    foreach (Auto<DisposablePipeline> pipeline in _computePipelineCache.Values)
                    {
                        pipeline.Dispose();
                    }
                }

                for (int i = 0; i < Templates.Length; i++)
                {
                    Templates[i]?.Dispose();
                }

                if (_dummyRenderPass.Value.Handle != 0)
                {
                    _dummyRenderPass.Dispose();
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
