Shader "Unlit/ZPlayerScreen"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}

        _MetaPassEmissiveBoost("Meta Pass Emissive Boost", Float) = 1.0
        _TargetAspectRatio("Target Aspect Ratio", Float) = 1.7777777
        [ToggleUI]_IsAVProInput("Is AV Pro Input", Int) = 0
        [ToggleUI]_Transparent("Transparent", Int) = 0
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            UNITY_DECLARE_TEX2D(_MainTex);
            float4 _MainTex_TexelSize;
            float _IsAVProInput;
            float _TargetAspectRatio;
            #define _EmissionColor float3(1, 1, 1)

            half4 VideoEmission(float2 uv)
            {
                float2 emissionRes = _MainTex_TexelSize.zw;


                float currentAspectRatio = emissionRes.x / emissionRes.y;

                float visibility = 1.0;

                // If the aspect ratio does not match the target ratio, then we fit the UVs to maintain the aspect ratio while fitting the range 0-1
                if (abs(currentAspectRatio - _TargetAspectRatio) > 0.001)
                {
                    float2 normalizedVideoRes = float2(emissionRes.x / _TargetAspectRatio, emissionRes.y);
                    float2 correctiveScale;
                    
                    // Find which axis is greater, we will clamp to that
                    if (normalizedVideoRes.x > normalizedVideoRes.y)
                        correctiveScale = float2(1, normalizedVideoRes.y / normalizedVideoRes.x);
                    else
                        correctiveScale = float2(normalizedVideoRes.x / normalizedVideoRes.y, 1);

                    uv = ((uv - 0.5) / correctiveScale) + 0.5;
                    uv = (uv - 0.01) * 1.02;

                    // Antialiasing on UV clipping
                    float2 uvPadding = (1 / emissionRes) * 0.1;
                    float2 uvfwidth = fwidth(uv.xy);
                    float2 maxFactor = smoothstep(uvfwidth + uvPadding + 1, uvPadding + 1, uv.xy);
                    float2 minFactor = smoothstep(-uvfwidth - uvPadding, -uvPadding, uv.xy);

                    visibility = maxFactor.x * maxFactor.y * minFactor.x * minFactor.y;

                    //if (any(uv <= 0) || any(uv >= 1))
                    //    return float3(0, 0, 0);
                }

                #if UNITY_UV_STARTS_AT_TOP
                if (_IsAVProInput)
                {
                    uv = float2(uv.x, 1 - uv.y);
                }
                #endif
                
                float3 texColor = UNITY_SAMPLE_TEX2D(_MainTex, uv).rgb;

            #ifndef UNITY_COLORSPACE_GAMMA
                if (_IsAVProInput)
                {
                    texColor = pow(texColor, 2.2f);
                }
            #endif

                return half4(texColor * _EmissionColor.rgb, visibility);
            }

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v); //Insert
                UNITY_INITIALIZE_OUTPUT(v2f, o); //Insert
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); //Insert

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half4 col = VideoEmission(i.uv);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}

// 
// MIT License

// Copyright (c) 2020 Merlin

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.