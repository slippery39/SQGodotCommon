shader_type canvas_item;

uniform float outline_width : hint_range(0.0, 10.0) = 2.0;
uniform vec4 outline_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;

void fragment() {
    vec2 screen_uv = SCREEN_UV;
    vec4 original = texture(SCREEN_TEXTURE, screen_uv);
    
    // Sample surrounding pixels
    float outline_alpha = 0.0;
    vec2 pixel_size = 1.0 / SCREEN_PIXEL_SIZE;
    
    for(float x = -outline_width; x <= outline_width; x += 1.0) {
        for(float y = -outline_width; y <= outline_width; y += 1.0) {
            if(x == 0.0 && y == 0.0) continue;
            
            vec2 offset = vec2(x, y) * pixel_size;
            vec4 sample_color = texture(SCREEN_TEXTURE, screen_uv + offset);
            outline_alpha = max(outline_alpha, sample_color.a);
        }
    }
	
    
    // If we have outline but no original content, show outline
    if(outline_alpha<0.9) {
        COLOR = outline_color;
    } else {
        COLOR = vec4(1,0,0,1);
    }
	
}