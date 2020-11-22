using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

class BilateralBlur : CustomPass {
  [Range(1, 30)]
  public float sigma = 10.0f;
  [Range(0.1f, 1)]
  public float bSigma = 0.1f;
  public bool useMask = false;
  public LayerMask maskLayer = 0;

  Material blurMaterial;
  RTHandle intermediateBuffer;
  RTHandle blurBuffer;
  RTHandle maskBuffer;
  RTHandle maskDepthBuffer;
  RTHandle colorCopy;
  ShaderTagId[] shaderTags;

  // Trick to always include these shaders in build
  [SerializeField, HideInInspector]
  Shader blurShader;
  [SerializeField, HideInInspector]
  Shader whiteRenderersShader;

  static class ShaderID {
    public static readonly int _BlitTexture = Shader.PropertyToID("_BlitTexture");
    public static readonly int _BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
    public static readonly int _BlitMipLevel = Shader.PropertyToID("_BlitMipLevel");
    public static readonly int _Sigma = Shader.PropertyToID("_Sigma");
    public static readonly int _BSigma = Shader.PropertyToID("_BSigma");
    public static readonly int _Source = Shader.PropertyToID("_Source");
    public static readonly int _ColorBufferCopy = Shader.PropertyToID("_ColorBufferCopy");
    public static readonly int _Mask = Shader.PropertyToID("_Mask");
    public static readonly int _MaskDepth = Shader.PropertyToID("_MaskDepth");
    public static readonly int _InvertMask = Shader.PropertyToID("_InvertMask");
  }

  // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
  // When empty this render pass will render to the active camera render target.
  // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
  // The render pipeline will ensure target setup and clearing happens in an performance manner.
  protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd) {
    if (blurBuffer == null) { blurShader = Shader.Find("Hidden/FullScreen/BilateralBlur"); }
    blurMaterial = CoreUtils.CreateEngineMaterial(blurShader);

    // Allocate the buffers used for the blur in half resolution to save some memory
    intermediateBuffer = RTHandles.Alloc(
      Vector2.one, TextureXR.slices, dimension: TextureXR.dimension,
      colorFormat: GraphicsFormat.B10G11R11_UFloatPack32, // We don't need alpha in the blur
      useDynamicScale: true, name: "IntermediateBuffer"
    );

    blurBuffer = RTHandles.Alloc(
      Vector2.one, TextureXR.slices, dimension: TextureXR.dimension,
      colorFormat: GraphicsFormat.B10G11R11_UFloatPack32, // We don't need alpha in the blur
      useDynamicScale: true, name: "BlurBuffer"
    );

    shaderTags = new ShaderTagId[4] {
      new ShaderTagId("Forward"),
      new ShaderTagId("ForwardOnly"),
      new ShaderTagId("SRPDefaultUnlit"),
      new ShaderTagId("FirstPass"),
    };
  }

  void AllocateMaskBuffersIfNeeded() {
    if (useMask) {
      if (colorCopy == null) {
        var hdrpAsset = (GraphicsSettings.renderPipelineAsset as HDRenderPipelineAsset);
        var colorBufferFormat = hdrpAsset.currentPlatformRenderPipelineSettings.colorBufferFormat;

        colorCopy = RTHandles.Alloc(
          Vector2.one, TextureXR.slices, dimension: TextureXR.dimension,
          colorFormat: (GraphicsFormat)colorBufferFormat,
          useDynamicScale: true, name: "Color Copy"
        );
      }
      if (maskBuffer == null) {
        maskBuffer = RTHandles.Alloc(
          Vector2.one, TextureXR.slices, dimension: TextureXR.dimension,
          colorFormat: GraphicsFormat.R8_UNorm, // We only need a 1 channel mask to composite the blur and color buffer copy
          useDynamicScale: true, name: "Blur Mask"
        );
      }
      if (maskDepthBuffer == null) {
        maskDepthBuffer = RTHandles.Alloc(
          Vector2.one, TextureXR.slices, dimension: TextureXR.dimension,
          colorFormat: GraphicsFormat.R16_UInt, useDynamicScale: true,
          name: "Blur Depth Mask", depthBufferBits: DepthBits.Depth16
        );
      }
    }
  }

  protected override void Execute(ScriptableRenderContext renderContext, CommandBuffer cmd, HDCamera hdCamera, CullingResults cullingResult) {
    AllocateMaskBuffersIfNeeded();
    if (blurMaterial != null) {
      if (useMask) { DrawMaskObjects(renderContext, cmd, hdCamera, cullingResult); }
      GenerateGaussianMips(cmd, hdCamera);
    }
  }

  protected override void AggregateCullingParameters(ref ScriptableCullingParameters cullingParameters, HDCamera hdCamera)
      => cullingParameters.cullingMask |= (uint)maskLayer.value;

  void DrawMaskObjects(ScriptableRenderContext renderContext, CommandBuffer cmd, HDCamera hdCamera, CullingResults cullingResult) {
    // Render the objects in the layer blur mask into a mask buffer with their materials so we keep the alpha-clip and transparency if there is any.
    var result = new RendererListDesc(shaderTags, cullingResult, hdCamera.camera) {
      rendererConfiguration = PerObjectData.None,
      renderQueueRange = RenderQueueRange.all,
      sortingCriteria = SortingCriteria.BackToFront,
      excludeObjectMotionVectors = false,
      layerMask = maskLayer,
      stateBlock = new RenderStateBlock(RenderStateMask.Depth) { depthState = new DepthState(true, CompareFunction.LessEqual) },
    };

    CoreUtils.SetRenderTarget(cmd, maskBuffer, maskDepthBuffer, ClearFlag.All);
    HDUtils.DrawRendererList(renderContext, cmd, RendererList.Create(result));
  }

  void GenerateGaussianMips(CommandBuffer cmd, HDCamera hdCam) {
    RTHandle source;

    // Retrieve the target buffer of the blur from the UI:
    if (targetColorBuffer == TargetBuffer.Camera) { GetCameraBuffers(out source, out _); }
    else { GetCustomBuffers(out source, out _); }

    // Save the non blurred color into a copy if the mask is enabled:
    if (useMask) { cmd.CopyTexture(source, colorCopy); }

    // Downsample
    using (new ProfilingScope(cmd, new ProfilingSampler("Downsample"))) {
      // This Blit will automatically downsample the color because our target buffer have been allocated in half resolution
      HDUtils.BlitCameraTexture(cmd, source, intermediateBuffer, 0);
    }

    // Horizontal Blur
    using (new ProfilingScope(cmd, new ProfilingSampler("H Blur"))) {
      var hBlurProperties = new MaterialPropertyBlock();
      hBlurProperties.SetFloat(ShaderID._Sigma, sigma);
      hBlurProperties.SetFloat(ShaderID._BSigma, bSigma);
      hBlurProperties.SetTexture(ShaderID._Source, intermediateBuffer); // The blur is 4 pixel wide in the shader
      HDUtils.DrawFullScreen(cmd, blurMaterial, blurBuffer, hBlurProperties, shaderPassId: 0); // Do not forget the shaderPassId: ! or it won't work
    }

    // Copy back the result in the color buffer while doing a vertical blur
    using (new ProfilingScope(cmd, new ProfilingSampler("V Blur + Copy back"))) {
      var vBlurProperties = new MaterialPropertyBlock();
      // When we use a mask, we do the vertical blur into the downsampling buffer instead of the camera buffer
      // We need that because we're going to write to the color buffer and read from this blured buffer which we can't do
      // if they are in the same buffer
      vBlurProperties.SetFloat(ShaderID._Sigma, sigma);
      vBlurProperties.SetFloat(ShaderID._BSigma, bSigma);
      vBlurProperties.SetTexture(ShaderID._Source, blurBuffer);
      var targetBuffer = (useMask) ? intermediateBuffer : source;
      HDUtils.DrawFullScreen(cmd, blurMaterial, targetBuffer, vBlurProperties, shaderPassId: 1); // Do not forget the shaderPassId: ! or it won't work
    }

    if (useMask) {
      // Merge the non blur copy and the blurred version using the mask buffers
      using (new ProfilingScope(cmd, new ProfilingSampler("Compose Mask Blur"))) {
        var compositingProperties = new MaterialPropertyBlock();
        compositingProperties.SetFloat(ShaderID._Sigma, sigma);
        compositingProperties.SetFloat(ShaderID._BSigma, bSigma);
        compositingProperties.SetTexture(ShaderID._Source, intermediateBuffer);
        compositingProperties.SetTexture(ShaderID._ColorBufferCopy, colorCopy);
        compositingProperties.SetTexture(ShaderID._Mask, maskBuffer);
        compositingProperties.SetTexture(ShaderID._MaskDepth, maskDepthBuffer);
        HDUtils.DrawFullScreen(cmd, blurMaterial, source, compositingProperties, shaderPassId: 2); // Do not forget the shaderPassId: ! or it won't work
      }
    }
  }

  // release all resources
  protected override void Cleanup() {
    CoreUtils.Destroy(blurMaterial);
    intermediateBuffer.Release();
    blurBuffer.Release();
    maskDepthBuffer?.Release();
    maskBuffer?.Release();
    colorCopy?.Release();
  }
}