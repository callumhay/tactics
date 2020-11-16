// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Tactics/Shaders/DrawVertStructBuffer" {
  Properties {
      _Color ("Color", Color) = (1,1,1,1)
      _MainTex ("Albedo (RGB)", 2D) = "white" {}
      _Glossiness ("Smoothness", Range(0,1)) = 0.5
      _Metallic ("Metallic", Range(0,1)) = 0.0
  }
  
	SubShader {
  Pass {
    Tags { "RenderType"="Opaque" }
		CGPROGRAM
    
    //#pragma surface surf Standard vertex:vert addshadow
    #pragma target 3.5
    #pragma vertex vert
    #pragma fragment frag
    #include "UnityCG.cginc"

    struct Vert {
      float3 position; 
      float3 normal;
    };
    
    StructuredBuffer<Vert> _Buffer;

    struct appInData {
      uint vid : SV_VertexID;
    };

    struct v2f {
      float4 vertex : POSITION;
      float3 normal : NORMAL;
      float4 tangent : TANGENT;
      float4 color : COLOR;
      float4 texcoord : TEXCOORD0;
      float4 texcoord1 : TEXCOORD1;
      float4 texcoord2 : TEXCOORD2;
      float4 texcoord3 : TEXCOORD3;
      
    };
    /*
    struct Input {
      float4 color : COLOR;
      float2 uv_MainTex;
    };
    */

    sampler2D _MainTex;
    half _Glossiness;
    half _Metallic;
    fixed4 _Color;

    void vert(in appInData input, out v2f v) {
      //UNITY_INITIALIZE_OUTPUT( Input, o );

      //#if defined(SHADER_API_D3D11) || defined(SHADER_API_METAL) || defined(SHADER_API_GLCORE)
      Vert currVert = _Buffer[input.vid];
      v.vertex = UnityObjectToClipPos(float4(currVert.position, 1));
      v.normal = currVert.normal;  
      //#else
      //v.vertex = float4(0,0,0,1);  
      //v.normal = float3(0,0,0);  
      //#endif

      v.color = float4(1,1,1,1);
    }

    float4 frag(v2f IN) : COLOR {
			return IN.color;
		}
    /*
    void surf(Input IN, inout SurfaceOutputStandard o) {
    
      // Metallic and smoothness come from slider variables
      o.Metallic = _Metallic;
      o.Smoothness = _Glossiness;

      // Albedo comes from a texture tinted by color
      fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
      o.Albedo = c.rgb;
      o.Alpha = c.a;
    }
    */
    ENDCG
	}
  }
}