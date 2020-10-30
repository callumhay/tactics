Shader "Custom/TerrainShader" {

    Properties {
      [NoScaleOffset] _GroundTex("Ground Texture", 2D) = "white" {}
      [NoScaleOffset] _WallTex("Wall Texture", 2D) = "white" {}

      [NoScaleOffset] _GroundNormalMap("Ground Normal Map", 2D) = "bump" {}
      [NoScaleOffset] _WallNormalMap("Wall Normal Map", 2D) = "bump" {}

      [NoScaleOffset] _OcclusionMap("Occlusion", 2D) = "white" {}

      _TexScale("Texture Scale", Float) = 1
      _TexOffsetX("Texture Offset X", Range(0,1)) = 0
      _TexOffsetY("Texture Offset Y", Range(0,1)) = 0

      _NormalMapStrength("Normal Map Strength", Range(0,1)) = 1.0
      _OcclusionStrength("Occlusion Strength", Range(0,1)) = 1.0
      _Glossiness("Smoothness", Range(0,1)) = 0.5
      [Gamma] _Metallic("Metallic", Range(0,1)) = 0
    }

    SubShader {
      Tags {"RenderType" = "Opaque"}

      CGPROGRAM
      #pragma surface surf Standard fullforwardshadows addshadow
      #pragma target 3.0

      #include "UnityStandardUtils.cginc"
      #define TRIPLANAR_CORRECT_PROJECTED_U // Flip UVs horizontally to correct for back side projection

      // Reoriented Normal Mapping
      // http://blog.selfshadow.com/publications/blending-in-detail/
      // Altered to take normals (-1 to 1 ranges) rather than unsigned normal maps (0 to 1 ranges)
      float3 blend_rnm(float3 n1, float3 n2) {
          n1.z += 1;
          n2.xy = -n2.xy;
          return n1 * dot(n1, n2) / n1.z - n2;
      }

      sampler2D _GroundTex;
      sampler2D _WallTex;
      sampler2D _GroundNormalMap;
      sampler2D _WallNormalMap;
      sampler2D _OcclusionMap;

      float _TexScale;
      float _TexOffsetX;
      float _TexOffsetY;

      half _OcclusionStrength;
      half _NormalMapStrength;
      half _Glossiness;
      half _Metallic;

      struct Input {
        float3 worldPos;
        float3 worldNormal;
        INTERNAL_DATA
      };

      float3 WorldToTangentNormalVector(Input IN, float3 normal) {
        float3 t2w0 = WorldNormalVector(IN, float3(1,0,0));
        float3 t2w1 = WorldNormalVector(IN, float3(0,1,0));
        float3 t2w2 = WorldNormalVector(IN, float3(0,0,1));
        float3x3 t2w = float3x3(t2w0, t2w1, t2w2);
        return normalize(mul(t2w, normal));
      }

      void surf(Input IN, inout SurfaceOutputStandard o) {
        IN.worldNormal = WorldNormalVector(IN, o.Normal);

        float3 scaledOffsetWorldPos = (IN.worldPos * _TexScale) + float3(_TexOffsetX, _TexOffsetY, 0);
        float3 pWeight = 7.0 * (abs(normalize(IN.worldNormal)) - 0.2);
        pWeight = max(pow(pWeight, 3), float3(1, 1, 1));
        pWeight /= max(dot(pWeight, 1), 0.0001);
        pWeight = saturate(pWeight);

        float2 uvX = scaledOffsetWorldPos.yz;
        float2 uvY = scaledOffsetWorldPos.xz;
        float2 uvZ = scaledOffsetWorldPos.xy;

        // Albedo
        float3 xProjAlbedo = tex2D(_WallTex, uvX) * pWeight.x;
        float3 yProjAlbedo = tex2D(_GroundTex, uvY) * pWeight.y;
        float3 zProjAlbedo = tex2D(_WallTex, uvZ) * pWeight.z;
        o.Albedo = xProjAlbedo + yProjAlbedo + zProjAlbedo;

        // Occlusion
        half occX = tex2D(_OcclusionMap, uvX).g * pWeight.x;
        half occY = tex2D(_OcclusionMap, uvY).g * pWeight.y;
        half occZ = tex2D(_OcclusionMap, uvZ).g * pWeight.z;
        half occ = LerpOneTo(occX + occY + occZ, _OcclusionStrength);
        o.Occlusion = occ;

        // Normal Mapping 

        // Minor optimization of sign(). prevents return value of 0
        half3 axisSign = IN.worldNormal < 0 ? -1 : 1;

        // Tangent space normal maps
        float3 tnormalX = UnpackNormal(tex2D(_WallNormalMap, uvX));
        float3 tnormalY = UnpackNormal(tex2D(_GroundNormalMap, uvY));
        float3 tnormalZ = UnpackNormal(tex2D(_WallNormalMap, uvZ));

        // Flip normal maps' x axis to account for flipped UVs
        #if defined(TRIPLANAR_CORRECT_PROJECTED_U)
          tnormalX.x *= axisSign.x;
          tnormalY.x *= axisSign.y;
          tnormalZ.x *= -axisSign.z;
        #endif

        float3 absVertNormal = abs(IN.worldNormal);
        // Swizzle world normals to match tangent space and apply reoriented normal mapping blend
        tnormalX = blend_rnm(float3(IN.worldNormal.zy, absVertNormal.x), tnormalX);
        tnormalY = blend_rnm(float3(IN.worldNormal.xz, absVertNormal.y), tnormalY);
        tnormalZ = blend_rnm(float3(IN.worldNormal.xy, absVertNormal.z), tnormalZ);

        // Apply world space sign to tangent space Z
        tnormalX.z *= axisSign.x;
        tnormalY.z *= axisSign.y;
        tnormalZ.z *= axisSign.z;

        float3 worldNormal = normalize((1-_NormalMapStrength) * IN.worldNormal + 
          _NormalMapStrength * (tnormalX.zyx * pWeight.x + tnormalY.xzy * pWeight.y + tnormalZ.xyz * pWeight.z)
        );
        o.Normal = WorldToTangentNormalVector(IN, worldNormal);

        o.Metallic = _Metallic;
        o.Smoothness = _Glossiness;
      }
      ENDCG
    }
    Fallback "Diffuse"
  
}
