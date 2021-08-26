Shader "Soil/PBR Full" {
	Properties{
		_MapScale("Scale", Float) = 1

		[NoScaleOffset]_BaseMap("Base Texture", 2D) = "white" {}

		[NoScaleOffset]_BumpMap("Normal/Bump Texture", 2D) = "bump" {}
		_BumpScale("Bump Scale", Float) = 1

		[NoScaleOffset]_PBRMap("PBR Texture", 2D) = "black" {}

		_Smoothness("Smoothness Multiplier", Float) = 1
	}
	SubShader{
		Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry"}

		HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

		CBUFFER_START(UnityPerMaterial)
		float _MapScale;
		float4 _BaseMap_ST;
		float _Smoothness;
		float _BumpScale;
		CBUFFER_END
		ENDHLSL

	Pass {
		Name "Forward"
		Tags { "LightMode" = "UniversalForward" }

		HLSLPROGRAM

		#pragma vertex vert
		#pragma fragment frag

		// URP Keywords
		#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
		#pragma multi_compile _ _SHADOWS_SOFT

		// Includes
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

		struct Vert
		{
			float4 position;
			float3 normal;
			int indicy;
		};

		struct Varyings {
			float4 positionCS				: SV_POSITION;
			float3 positionWS				: TEXCOORD0;
			float3 normalWS					: TEXCOORD1;
			float3 viewDirWS 				: TEXCOORD2;
		};

		uniform StructuredBuffer<Vert> vertBuffer;
		uniform StructuredBuffer<int> triBuffer;

		Varyings vert(uint id : SV_VertexID) {
			Varyings OUT;
			Vert vertData = vertBuffer[triBuffer[id]];
			OUT.positionWS = vertData.position.xyz;
			OUT.positionCS = TransformObjectToHClip(vertData.position.xyz);
			OUT.viewDirWS = GetWorldSpaceViewDir(vertData.position.xyz);
			OUT.normalWS = TransformObjectToWorldNormal(vertData.normal);

			return OUT;
		}

		TEXTURE2D(_PBRMap); 	SAMPLER(sampler_PBRMap);

		half3 blend_rnm(half3 n1, half3 n2)
		{
			n1.z += 1;
			n2.xy = -n2.xy;

			return n1 * dot(n1, n2) / n1.z - n2;
		}

		half4 frag(Varyings IN) : SV_Target {
			//SURFACEDATA
			SurfaceData surfaceData = (SurfaceData)0;

			half3 blendingFactor = saturate(pow(IN.normalWS, 4));
			blendingFactor /= max(dot(blendingFactor, half3(1, 1, 1)), 0.0001);

			// calculate triplanar uvs
			float2 uvX = IN.positionWS.yz * _MapScale;
			float2 uvY = IN.positionWS.zx * _MapScale;
			float2 uvZ = IN.positionWS.xy * _MapScale;

			// offset UVs to prevent obvious mirroring
			uvY += 0.33;
			uvZ += 0.67;

			// minor optimization of sign(). prevents return value of 0
			half3 axisSign = IN.normalWS < 0 ? -1 : 1;

			// flip UVs horizontally to correct for back side projection
			uvX.x *= axisSign.x;
			uvY.x *= axisSign.y;
			uvZ.x *= -axisSign.z;

			half3 cx = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvX).rgb * blendingFactor.x;
			half3 cy = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvY).rgb * blendingFactor.y;
			half3 cz = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvZ).rgb * blendingFactor.z;
			surfaceData.albedo = cx + cy + cz;

			half3 pbrx = SAMPLE_TEXTURE2D(_PBRMap, sampler_PBRMap, uvX).rgb * blendingFactor.x;
			half3 pbry = SAMPLE_TEXTURE2D(_PBRMap, sampler_PBRMap, uvY).rgb * blendingFactor.y;
			half3 pbrz = SAMPLE_TEXTURE2D(_PBRMap, sampler_PBRMap, uvZ).rgb * blendingFactor.z;
			half3 pbr = pbrx + pbry + pbrz;

			surfaceData.smoothness = pbr.r * _Smoothness;
			surfaceData.metallic = pbr.g;
			surfaceData.occlusion = LerpWhiteTo(pbr.b, 1.0);

			//INPUTDATA
			InputData inputData = (InputData)0;

			inputData.positionWS = IN.positionWS;

			// tangent space normal maps
			half3 nxT = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvX), _BumpScale) * blendingFactor.x;
			half3 nyT = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvY), _BumpScale) * blendingFactor.y;
			half3 nzT = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvZ), _BumpScale) * blendingFactor.z;

			// flip normal maps' x axis to account for flipped UVs
			nxT.x *= axisSign.x;
			nyT.x *= axisSign.y;
			nzT.x *= -axisSign.z;

			half3 absVertNormal = abs(IN.normalWS);

			//swizzle world normals to match tangent space and apply reoriented normal mapping blend
			half3 nx = blend_rnm(half3(IN.normalWS.zy, absVertNormal.x), nxT);
			half3 ny = blend_rnm(half3(IN.normalWS.xz, absVertNormal.y), nyT);
			half3 nz = blend_rnm(half3(IN.normalWS.xy, absVertNormal.z), nzT);

			// apply world space sign to tangent space Z
			nx.z *= axisSign.x;
			ny.z *= axisSign.y;
			nz.z *= axisSign.z;

			half3 normal = normalize(nx + ny + nz + IN.normalWS);

			inputData.normalWS = normal;
			inputData.viewDirectionWS = IN.viewDirWS;
			inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);

			half4 color = UniversalFragmentPBR(inputData, surfaceData);

			return color;
		}
		ENDHLSL
		}
	}
}