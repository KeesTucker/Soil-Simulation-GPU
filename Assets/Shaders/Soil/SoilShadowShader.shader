Shader "Soil/Shadow" {
	SubShader{
		Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry"}

		HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

		CBUFFER_START(UnityPerMaterial)
		float4 _BaseMap_ST;
		float4 _BaseColor;
		float _Cutoff;
		CBUFFER_END
		ENDHLSL

		Pass {
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }

			ZWrite On
			ZTest LEqual

			HLSLPROGRAM

			// GPU Instancing
			#pragma multi_compile_instancing
			#pragma multi_compile _ DOTS_INSTANCING_ON

			#pragma vertex ProceduralShadowPassVertex
			#pragma fragment ShadowPassFragment

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"

			struct Vert
			{
				float4 position;
				float3 normal;
				int indicy;
			};
			uniform StructuredBuffer<Vert> vertBuffer;
			uniform StructuredBuffer<int> triBuffer;

			Varyings ProceduralShadowPassVertex(uint id : SV_VertexID) {
				Varyings output = (Varyings)0;
				Attributes input = (Attributes)0;

				input.positionOS = vertBuffer[triBuffer[id]].position;
				input.normalOS = vertBuffer[triBuffer[id]].normal;

				output.positionCS = GetShadowPositionHClip(input);
				return output;
			}

			ENDHLSL
		}
	}
}