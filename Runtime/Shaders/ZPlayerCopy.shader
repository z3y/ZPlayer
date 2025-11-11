Shader "Unlit/ZPlayerCopy"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
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

            half4 frag (v2f_customrendertexture i) : SV_Target
            {
                float2 uv = i.localTexcoord.xy;
                
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
                        texColor.r = GammaToLinearSpaceExact(texColor.r);
                        texColor.g = GammaToLinearSpaceExact(texColor.g);
                        texColor.b = GammaToLinearSpaceExact(texColor.b);
                    }
                #endif

                texColor = saturate(texColor);

                return float4(texColor, 1);
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