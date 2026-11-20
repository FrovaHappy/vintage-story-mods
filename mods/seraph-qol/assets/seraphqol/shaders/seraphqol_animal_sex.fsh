#version 330 core

uniform sampler2D iconTexture;
uniform vec4 iconTint;

in vec2 uv;
layout(location = 0) out vec4 outColor;

void main()
{
    vec4 tex = texture(iconTexture, uv);
    if (tex.a < 0.04) discard;
    tex.rgb /= tex.a;
    outColor = tex * iconTint;
    if (outColor.a < 0.01) discard;
}
