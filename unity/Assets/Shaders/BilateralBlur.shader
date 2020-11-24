﻿Shader "Hidden/FullScreen/BilateralBlur" {
  HLSLINCLUDE

  #pragma vertex Vert

  #pragma target 4.5
  #pragma only_renderers d3d11 ps4 xboxone vulkan metal switch
  #pragma enable_d3d11_debug_symbols

  #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

  // The PositionInputs struct allow you to retrieve a lot of useful information for your fullScreenShader:
  // struct PositionInputs
  // {
  //     float3 positionWS;  // World space position (could be camera-relative)
  //     float2 positionNDC; // Normalized screen coordinates within the viewport    : [0, 1) (with the half-pixel offset)
  //     uint2  positionSS;  // Screen space pixel coordinates                       : [0, NumPixels)
  //     uint2  tileCoord;   // Screen tile coordinates                              : [0, NumTiles)
  //     float  deviceDepth; // Depth from the depth buffer                          : [0, 1] (typically reversed)
  //     float  linearDepth; // View space Z coordinate                              : [Near, Far]
  // };

  // To sample custom buffers, you have access to these functions:
  // But be careful, on most platforms you can't sample to the bound color buffer. It means that you
  // can't use the SampleCustomColor when the pass color buffer is set to custom (and same for camera the buffer).
  // float3 SampleCustomColor(float2 uv);
  // float3 LoadCustomColor(uint2 pixelCoords);
  // float LoadCustomDepth(uint2 pixelCoords);
  // float SampleCustomDepth(float2 uv);

  // There are also a lot of utility function you can use inside Common.hlsl and Color.hlsl,
  // you can check them out in the source code of the core SRP package.

  #define KERNEL_SIZE 4
  #define KERNEL_TAPS (2*KERNEL_SIZE+1)

  TEXTURE2D_X(_Source);
  TEXTURE2D_X(_ColorBufferCopy);
  TEXTURE2D_X_HALF(_Mask);
  TEXTURE2D_X_HALF(_MaskDepth);
  float4 _ViewPortSize; // We need the viewport size because we have a non fullscreen render target (blur buffers are downsampled in half res)
  float _Radius;
  float _InvertMask;
  float _Sigma;
  float _BSigma;

  #pragma enable_d3d11_debug_symbols

  float normPdf(float x, float sigma) {
    return 0.39894*exp(-0.5*x*x/(sigma*sigma))/sigma;
  }
  float normPdf3(float3 v, float sigma) {
    return 0.39894*exp(-0.5*dot(v,v)/(sigma*sigma))/sigma;
  }

  // We need to clamp the UVs to avoid bleeding from bigger render tragets (when we have multiple cameras)
  float2 ClampUVs(float2 uv) {
    uv = clamp(uv, 0, _RTHandleScale - _ScreenSize.zw * 2); // clamp UV to 1 pixel to avoid bleeding
    return uv;
  }

  float2 GetSampleUVs(Varyings varyings) {
    float depth = LoadCameraDepth(varyings.positionCS.xy);
    PositionInputs posInput = GetPositionInput(varyings.positionCS.xy, _ViewPortSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);
    return posInput.positionNDC.xy * _RTHandleScale.xy;
  }

  float4 HorizontalBlur(Varyings varyings) : SV_Target {
    float2 texcoord = GetSampleUVs(varyings);
    float2 offset = _ScreenSize.zw * _Radius;
    float3 colour = SAMPLE_TEXTURE2D_X_LOD(_Source, s_linear_clamp_sampler, texcoord, 0).rgb;
    float depth = LoadCameraDepth(varyings.positionCS.xy);

    float kernel[KERNEL_TAPS];
    float3 finalColour = float3(0,0,0);
    for (int k = 0; k <= KERNEL_SIZE; k++) {
      kernel[KERNEL_SIZE+k] = kernel[KERNEL_SIZE-k] = normPdf(k, _Sigma);
    }

    float2 uv;
    float3 sampleColour;
    float sampleDepth;
    float factor;

    float bZ = 1.0 / normPdf(0.0, _BSigma);
    float factorSum = 0.0;
    for (int i = -KERNEL_SIZE; i <= KERNEL_SIZE; i++) {
      uv = ClampUVs(texcoord + float2(i, 0) * offset);
      sampleColour = SAMPLE_TEXTURE2D_X_LOD(_Source, s_linear_clamp_sampler, uv, 0).rgb;
      sampleDepth = LoadCameraDepth(uv);
      factor = normPdf3(sampleColour-colour, _BSigma) * normPdf(sampleDepth-depth, _Sigma) * bZ * kernel[KERNEL_SIZE+i];
      factorSum += factor;
      finalColour += factor*sampleColour;
    }

    return float4(finalColour/factorSum, 1);
  }

  float4 VerticalBlur(Varyings varyings) : SV_Target {
    float2 texcoord = GetSampleUVs(varyings);
    float2 offset = _ScreenSize.zw * _Radius;
    float3 colour = SAMPLE_TEXTURE2D_X_LOD(_Source, s_linear_clamp_sampler, texcoord, 0).rgb;
    float depth = LoadCameraDepth(varyings.positionCS.xy);

    float kernel[KERNEL_TAPS];
    float3 finalColour = float3(0,0,0);
    for (int k = 0; k <= KERNEL_SIZE; k++) {
      kernel[KERNEL_SIZE+k] = kernel[KERNEL_SIZE-k] = normPdf(k, _Sigma);
    }

    float2 uv;
    float3 sampleColour;
    float sampleDepth;
    float factor;

    float bZ = 1.0 / normPdf(0.0, _BSigma);
    float factorSum = 0.0;
    for (int i = -KERNEL_SIZE; i <= KERNEL_SIZE; i++) {
      uv = ClampUVs(texcoord + float2(0, i) * offset);
      sampleColour = SAMPLE_TEXTURE2D_X_LOD(_Source, s_linear_clamp_sampler, uv, 0).rgb;
      sampleDepth = LoadCameraDepth(uv);
      factor = normPdf3(sampleColour-colour, _BSigma) * normPdf(sampleDepth-depth, _Sigma) * bZ * kernel[KERNEL_SIZE+i];
      factorSum += factor;
      finalColour += factor*sampleColour;
    }

    return float4(finalColour/factorSum, 1);
  }

  float4 CompositeMaskedBlur(Varyings varyings) : SV_Target {
    float depth = LoadCameraDepth(varyings.positionCS.xy);
    float2 uv = ClampUVs(GetSampleUVs(varyings));

    float4 colorBuffer = SAMPLE_TEXTURE2D_X_LOD(_ColorBufferCopy, s_linear_clamp_sampler, uv, 0).rgba;
    float4 blurredBuffer = SAMPLE_TEXTURE2D_X_LOD(_Source, s_linear_clamp_sampler, uv, 0).rgba;
    float4 mask = SAMPLE_TEXTURE2D_X_LOD(_Mask, s_linear_clamp_sampler, uv, 0);
    float maskDepth = SAMPLE_TEXTURE2D_X_LOD(_MaskDepth, s_linear_clamp_sampler, uv, 0).r;
    float maskValue = any(mask.rgb > 0.01) || (maskDepth > depth - 1e-4 && maskDepth != 0);
    if (_InvertMask > 0.5) { maskValue = !maskValue; }
    return float4(lerp(blurredBuffer.rgb, colorBuffer.rgb, maskValue*mask.a), colorBuffer.a);
  }

  ENDHLSL

  SubShader {
    Pass {
      Name "Horizontal Blur"

      ZWrite Off
      ZTest Always
      Blend Off
      Cull Off

      HLSLPROGRAM
          #pragma fragment HorizontalBlur
      ENDHLSL
    }

    Pass {
      Name "Vertical Blur"

      ZWrite Off
      ZTest Always
      Blend Off
      Cull Off

      HLSLPROGRAM
          #pragma fragment VerticalBlur
      ENDHLSL
    }

    Pass {
      // Vertical Blur from the blur buffer back to camera color
      Name "Composite Blur and Color using a mask"

      ZWrite Off
      ZTest Always
      Blend Off
      Cull Off

      HLSLPROGRAM
          #pragma fragment CompositeMaskedBlur
      ENDHLSL
    }
  }
  Fallback Off
}
