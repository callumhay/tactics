Shader "Custom/TerrainShader" {

    Properties {
      _GroundTex("Ground Texture", 2D) = "white" {}
      _WallTex("Wall Texture", 2D) = "white" {}

      _TexScale("Texture Scale", Float) = 1
    }

    SubShader {
      Tags {"RenderType" = "Opaque"}

      CGPROGRAM
      #pragma surface surf Standard fullforwardshadows addshadow

      sampler2D _GroundTex;
      sampler2D _WallTex;
      float _TexScale;

      struct Input {
        float3 worldPos;
        float3 worldNormal;
      };

      void surf(Input IN, inout SurfaceOutputStandard o) {
        float3 scaledWorldPos = IN.worldPos / _TexScale;
        float3 pWeight = 7.0 * (abs(normalize(IN.worldNormal)) - 0.2);
        pWeight = max(pow(pWeight, float3(3, 3, 3)), float3(1, 1, 1));
        pWeight /= dot(pWeight, 1);

        float3 xProj = tex2D(_WallTex, scaledWorldPos.yz) * pWeight.x;
        float3 yProj = tex2D(_GroundTex, scaledWorldPos.xz) * pWeight.y;
        float3 zProj = tex2D(_WallTex, scaledWorldPos.xy) * pWeight.z;

        o.Albedo = xProj + yProj + zProj;
      }
      ENDCG
    }
    Fallback "Diffuse"
  
}
