using SuperSwitch.Common.Logging;
using SuperSwitch.Graphics.GAL;
using SuperSwitch.Graphics.Shader;
using SuperSwitch.Graphics.Shader.Translation;
using Silk.NET.Vulkan;
using System;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace SuperSwitch.Graphics.Vulkan
{
    /// <summary>
    /// SuperSwitch: GPU ASTC decoder (compute shader port of SuperSwitch.Graphics.Texture.Astc.AstcDecoder).
    /// </summary>
    partial class HelperShader
    {
        private const int AstcWorkgroupSize = 64;
        private const int AstcMaxGroupsX = 65535;

        private ShaderCollection _programAstcDecode;

        public bool IsAstcDecodeReady => _programAstcDecode != null;

        public bool InitializeAstcDecoder(VulkanRenderer gd)
        {
            try
            {
                ResourceLayout astcResourceLayout = new ResourceLayoutBuilder()
                    .Add(ResourceStages.Compute, ResourceType.UniformBuffer, 0)
                    .Add(ResourceStages.Compute, ResourceType.StorageBuffer, 1)
                    .Add(ResourceStages.Compute, ResourceType.StorageBuffer, 2, true)
                    .Add(ResourceStages.Compute, ResourceType.StorageBuffer, 3).Build();

                ShaderCollection program = gd.CreateProgramWithMinimalLayout([
                    new ShaderSource(ReadSpirv("AstcDecode.spv"), ShaderStage.Compute, TargetLanguage.Spirv)
                ], astcResourceLayout);

                if (!program.IsLinked)
                {
                    program.Dispose();

                    Logger.Warning?.Print(LogClass.Gpu, "GPU ASTC decoder failed to link, using the CPU decoder.");

                    return false;
                }

                _programAstcDecode = program;

                return true;
            }
            catch (Exception ex)
            {
                Logger.Warning?.Print(LogClass.Gpu, $"GPU ASTC decoder unavailable, using the CPU decoder: {ex.Message}");

                return false;
            }
        }

        /// <summary>
        /// Decodes ASTC blocks from <paramref name="input"/> into RGBA8 texels in <paramref name="output"/>.
        /// The output buffer is left ready for a transfer read.
        /// </summary>
        public void DecodeAstc(
            VulkanRenderer gd,
            CommandBufferScoped cbs,
            BufferHolder input,
            BufferHolder levelInfo,
            BufferHolder output,
            int outputSize,
            int blockWidth,
            int blockHeight,
            int totalBlocks,
            int entryCount)
        {
            Auto<DisposableBuffer> inputAuto = input.GetBuffer();
            Auto<DisposableBuffer> outputAuto = output.GetBuffer();
            Auto<DisposableBuffer> levelAuto = levelInfo.GetBuffer();

            // Keep the buffers alive until the command buffer completes.
            inputAuto.Get(cbs);
            levelAuto.Get(cbs);

            Buffer dstBuffer = outputAuto.Get(cbs, 0, outputSize, true).Value;

            BufferHolder.InsertBufferBarrier(
                gd,
                cbs.CommandBuffer,
                dstBuffer,
                BufferHolder.DefaultAccessFlags,
                AccessFlags.ShaderWriteBit,
                PipelineStageFlags.AllCommandsBit,
                PipelineStageFlags.ComputeShaderBit,
                0,
                outputSize);

            int groups = (totalBlocks + AstcWorkgroupSize - 1) / AstcWorkgroupSize;
            int groupsX = Math.Min(groups, AstcMaxGroupsX);
            int groupsY = (groups + groupsX - 1) / groupsX;

            const int ParamsBufferSize = 32;

            Span<int> shaderParams = stackalloc int[ParamsBufferSize / sizeof(int)];

            shaderParams[0] = blockWidth;
            shaderParams[1] = blockHeight;
            shaderParams[2] = totalBlocks;
            shaderParams[3] = entryCount;
            shaderParams[4] = groupsX * AstcWorkgroupSize;

            using ScopedTemporaryBuffer buffer = gd.BufferManager.ReserveOrCreate(gd, cbs, ParamsBufferSize);

            buffer.Holder.SetDataUnchecked<int>(buffer.Offset, shaderParams);

            _pipeline.SetCommandBuffer(cbs);

            _pipeline.SetUniformBuffers([new BufferAssignment(0, buffer.Range)]);

            Span<Auto<DisposableBuffer>> sbRanges = new Auto<DisposableBuffer>[3];

            sbRanges[0] = inputAuto;
            sbRanges[1] = outputAuto;
            sbRanges[2] = levelAuto;

            _pipeline.SetStorageBuffers(1, sbRanges);

            _pipeline.SetProgram(_programAstcDecode);
            _pipeline.DispatchCompute(groupsX, groupsY, 1);

            _pipeline.Finish(gd, cbs);

            BufferHolder.InsertBufferBarrier(
                gd,
                cbs.CommandBuffer,
                dstBuffer,
                AccessFlags.ShaderWriteBit,
                AccessFlags.TransferReadBit,
                PipelineStageFlags.ComputeShaderBit,
                PipelineStageFlags.TransferBit,
                0,
                outputSize);
        }
    }
}
