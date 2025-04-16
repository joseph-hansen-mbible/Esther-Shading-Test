Shader "Hidden/BlitTexture2DArray"
{
    SubShader
    {
        // Basic pass setup for post-processing/blit
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "Blit Slice" // Optional pass name

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // Include necessary SRP library files
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl" // Common.hlsl is usually sufficient
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Sampling/Sampling.hlsl" // For sampling macros
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl" // For instancing macros
            // #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonVertex.hlsl" // REMOVE THIS LINE


            // Input structure matching a fullscreen triangle/quad (SRP style)
            struct Attributes
            {
                // Use vertex ID to generate fullscreen triangle
                uint vertexID           : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID // Required for instancing
            };

            // Output structure (Varyings) (SRP style)
            struct Varyings
            {
                float4 positionCS       : SV_POSITION; // Clip space position
                float2 uv               : TEXCOORD0; // Pass UVs to fragment
                UNITY_VERTEX_OUTPUT_STEREO // Required for stereo rendering
            };

            // Texture and Sampler declarations (correct for SRP)
            TEXTURE2D_ARRAY(_MainTex); // Use SRP macro for declaration
            SAMPLER(sampler_MainTex); // Use SRP macro for sampler declaration

            // Slice index property
            int _SliceIndex;

            // Vertex Shader (SRP style - Manual Fullscreen Triangle)
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input); // Setup instancing
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output); // Setup stereo rendering

                // Manually calculate clip space position and UV for a fullscreen triangle
                // Generates vertices at (-1,-1), (-1,3), (3,-1) which covers the screen
                float x = -1.0 + float((input.vertexID & 1) << 2);
                float y = -1.0 + float((input.vertexID & 2) << 1);
                // Derive UVs from clip space positions
                float2 uv = float2(x * 0.5 + 0.5, y * 0.5 + 0.5);

                output.positionCS = float4(x, y, 0.0, 1.0); // Use calculated clip space pos
                output.uv = uv; // Use calculated UVs

                // Adjust UVs for different platforms if necessary (often handled by Blit commands)
                #if UNITY_UV_STARTS_AT_TOP
                output.uv.y = 1.0 - output.uv.y;
                #endif

                return output;
            }

            // Fragment Shader (SRP style)
            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input); // Setup stereo rendering for fragment

                // Sample the texture array using the SRP macro with LOD
                // Use 0.0f for base mip level
                // Note: SAMPLE_TEXTURE2D_ARRAY_LOD takes uv (float2) and slice (float) separately
                return SAMPLE_TEXTURE2D_ARRAY_LOD(_MainTex, sampler_MainTex, input.uv, (float)_SliceIndex, 0.0f);
            }
            ENDHLSL
        }
    }
    Fallback Off // No fallback needed
}
