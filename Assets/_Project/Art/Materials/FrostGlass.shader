Shader "Custom/VR_FrostGlass_BoxProjection_HQ_FixProjection"
{
    Properties
    {
        _MainTex("Base Texture", 2D) = "white" {}
        _Color("Tint Color", Color) = (1,1,1,0.5)
        _Frost("Frost Intensity", Range(0,1)) = 0.25
        _Blur("Blur Radius", Range(0,3)) = 1
        _OffsetScale("Reflection Offset Scale", Range(0,1)) = 0.3
        _ReflDistance("Reflection Distance", Range(0.1,10)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        GrabPass { "_GrabTexture" }

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _GrabTexture;
            float4 _GrabTexture_TexelSize; // x = 1/width, y = 1/height

            float4 _MainTex_ST;
            fixed4 _Color;
            float _Frost;
            float _Blur;
            float _OffsetScale;
            float _ReflDistance;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 grabPos : TEXCOORD1;    // original fragment's grab pos (not used for refl sampling)
                float3 worldNormal : TEXCOORD2;
                float3 worldPos : TEXCOORD3;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                o.grabPos = ComputeGrabScreenPos(o.pos);
                return o;
            }

            // ガウス重み (簡易)
            inline float gaussianWeight(float x, float y, float sigma)
            {
                float r2 = x*x + y*y;
                return exp(-r2 / (2.0 * sigma * sigma));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // base color
                fixed4 baseCol = tex2D(_MainTex, i.uv);

                // camera/view
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);

                // reflection direction (use normal from mesh)
                float3 N = normalize(i.worldNormal);
                if (length(N) < 1e-4) N = float3(0,1,0);
                float3 refl = reflect(-viewDir, N);

                // --- 投影ベースの反射サンプリング (歪み対策) ---
                // 反射先のワールド座標を作る（距離はプロパティで調整）
                float3 reflWorldPos = i.worldPos + refl * _ReflDistance;

                // ワールド->クリップ空間（ViewProjection 行列を使用）
                float4 reflClipPos = mul(UNITY_MATRIX_VP, float4(reflWorldPos, 1.0));

                // ComputeGrabScreenPos に投げて tex2Dproj 用の値を得る
                float4 reflGrabPos = ComputeGrabScreenPos(reflClipPos);

                // ここで reflGrabPos.xy はプロジェクト済み座標、.w は投影スケールに使う
                // オフセットは画面上の実距離に対してテクセル単位で加算する（w を掛ける）
                float2 texel = _GrabTexture_TexelSize.xy;
                // kernel
                const int K = 2;
                float sigma = max(0.5, _Blur);

                float3 accum = 0;
                float wsum = 0;

                // サンプリング：オフセット値は reflGrabPos.w を掛けて投影空間スケールに変換
                for (int y = -K; y <= K; ++y)
                {
                    for (int x = -K; x <= K; ++x)
                    {
                        float fx = (float)x;
                        float fy = (float)y;
                        float w = gaussianWeight(fx, fy, sigma);

                        // sample offset in UV (screen) using texel and optional small screen offset
                        float2 sampleOffsetUV = float2(fx * texel.x, fy * texel.y);

                        // apply optional small reflect-direction bias in screen-space (scaled by OffsetScale)
                        // we compute a tiny bias in UV units from refl.xy but convert by reflGrabPos.w
                        float2 reflectBiasUV = refl.xy * (_OffsetScale * texel.x); // use texel.x as base; small
                        // combine
                        float2 finalOffset = sampleOffsetUV + reflectBiasUV;

                        // tex2Dproj requires projection-space offset scaled by reflGrabPos.w
                        float4 projPos = reflGrabPos + float4(finalOffset * reflGrabPos.w, 0, 0);

                        fixed4 s = tex2Dproj(_GrabTexture, projPos);
                        accum += s.rgb * w;
                        wsum += w;
                    }
                }

                float3 blurCol = accum / max(wsum, 1e-6);

                // Frost: 白寄せ（灰色化対策）
                blurCol = lerp(blurCol, float3(1.0,1.0,1.0), saturate(_Frost));

                // Mix base and reflection using alpha as blend factor
                float alphaBlend = saturate(_Color.a);
                float3 mixed = lerp(baseCol.rgb, blurCol, alphaBlend);

                // Apply tint color
                mixed *= _Color.rgb;

                float outA = _Color.a;

                return fixed4(mixed, outA);
            }

            ENDCG
        }
    }

    FallBack "Transparent/Diffuse"
}