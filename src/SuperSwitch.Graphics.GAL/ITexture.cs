using SuperSwitch.Common.Memory;

namespace SuperSwitch.Graphics.GAL
{
    public interface ITexture
    {
        int Width { get; }
        int Height { get; }

        void CopyTo(ITexture destination, int firstLayer, int firstLevel);
        void CopyTo(ITexture destination, int srcLayer, int dstLayer, int srcLevel, int dstLevel);
        void CopyTo(ITexture destination, Extents2D srcRegion, Extents2D dstRegion, bool linearFilter);
        void CopyTo(BufferRange range, int layer, int level, int stride);

        ITexture CreateView(TextureCreateInfo info, int firstLayer, int firstLevel);

        PinnedSpan<byte> GetData();
        PinnedSpan<byte> GetData(int layer, int level);

        /// <summary>
        /// Sets the texture data. The data passed as a <see cref="MemoryOwner{Byte}" /> will be disposed when
        /// the operation completes.
        /// </summary>
        /// <param name="data">Texture data bytes</param>
        void SetData(MemoryOwner<byte> data);

        /// <summary>
        /// Sets the texture data. The data passed as a <see cref="MemoryOwner{Byte}" /> will be disposed when
        /// the operation completes.
        /// </summary>
        /// <param name="data">Texture data bytes</param>
        /// <param name="layer">Target layer</param>
        /// <param name="level">Target level</param>
        void SetData(MemoryOwner<byte> data, int layer, int level);

        /// <summary>
        /// Sets the texture data. The data passed as a <see cref="MemoryOwner{Byte}" /> will be disposed when
        /// the operation completes.
        /// </summary>
        /// <param name="data">Texture data bytes</param>
        /// <param name="layer">Target layer</param>
        /// <param name="level">Target level</param>
        /// <param name="region">Target sub-region of the texture to update</param>
        void SetData(MemoryOwner<byte> data, int layer, int level, Rectangle<int> region);

        /// <summary>
        /// SuperSwitch: uploads ASTC compressed data, decoding it to RGBA8 on the host GPU.
        /// The data is consumed (and disposed when the operation completes) only if this returns true.
        /// </summary>
        /// <param name="data">ASTC blocks in linear order (levels outer, layers inner)</param>
        /// <param name="info">ASTC block size and dimensions of the data</param>
        /// <param name="layer">Target layer (only used if <paramref name="singleSlice"/> is true)</param>
        /// <param name="level">Target level (only used if <paramref name="singleSlice"/> is true)</param>
        /// <param name="singleSlice">True to update a single layer/level, false to update the whole texture</param>
        /// <returns>True if the upload was accepted</returns>
        bool TrySetDataAstc(MemoryOwner<byte> data, AstcUploadInfo info, int layer, int level, bool singleSlice) => false;

        void SetStorage(BufferRange buffer);

        void Release();
    }
}
