Shader "Unlit/ZPlayerCopy"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
        _TargetAspectRatio("Target Aspect Ratio", Float) = 1.7777777
        [ToggleUI]_IsAVProInput("Is AV Pro Input", Int) = 0
    }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex CustomRenderTextureVertexShader
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "UnityCustomRenderTexture.cginc"

            UNITY_DECLARE_TEX2D(_MainTex);
            float4 _MainTex_TexelSize;
            float _IsAVProInput;
            float2 _Resolution;
            #define _TargetAspectRatio float(1.7777777)

            half4 frag (v2f_customrendertexture i) : SV_Target
            {
                //float2 emissionRes = _MainTex_TexelSize.zw;
                float2 emissionRes = _Resolution;
                float2 uv = i.localTexcoord.xy;


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

                return float4(texColor * visibility, visibility);
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