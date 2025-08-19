shader_type canvas_item;

// Outline uniforms
uniform bool allow_out_of_bounds = true;
uniform float outline_thickness: hint_range(0.0, 16.0, 1.0) = 1.0;
uniform vec4 outline_color: source_color = vec4(1.0);

// Damage flash uniforms
uniform vec4 flash_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float flash_intensity : hint_range(0.0, 1.0) = 0.0;

// Helper function for outline
bool is_inside_usquare(vec2 x) {
	return x == clamp(x, vec2(0.0), vec2(1.0));
}

// Apply damage flash to base sprite color
vec4 apply_damage_flash(vec4 sprite_color) {
    if (flash_intensity <= 0.0) {
        return sprite_color;
    }
	
	if (sprite_color.a <=0.0)
	{
		return sprite_color;
	}
    
    return mix(sprite_color, flash_color, flash_intensity);
}

// Apply outline effect to the already-flashed sprite
vec4 apply_outline(vec2 uv, sampler2D tex, vec2 pixel_size) {
    if (outline_thickness <= 0.0 || outline_color.a <= 0.0) {
        vec4 base_color = texture(tex, uv);
        return apply_damage_flash(base_color);
    }

    vec2 modified_uv = uv;
    vec4 texture_color;

    if (allow_out_of_bounds) {
        vec2 pixel_size_2 = vec2(1.0) / (vec2(1.0) / pixel_size + vec2(outline_thickness * 2.0));
        modified_uv = (uv - pixel_size_2 * outline_thickness) * pixel_size / pixel_size_2;
        if (is_inside_usquare(modified_uv)) {
            texture_color = texture(tex, modified_uv);
        } else {
            texture_color = vec4(0.0);
        }
    } else {
        texture_color = texture(tex, uv);
    }

    // Apply damage flash to the base sprite first
    vec4 flashed_color = apply_damage_flash(texture_color);

    // If the current pixel already has content, return the flashed color
    //Higher values here reduce the artifact from semi transparent pixels due to anti-aliasing.
    if (texture_color.a > 0.9) {
        return flashed_color;
    }

    // Look for outline pixels - but we need to check the original texture for outline generation
    float alpha = 0.0;

    for (float y = 1.0; y <= outline_thickness; y++) {
        for (float x = 0.0; x <= y; x++) {
            if (length(vec2(x, y - 0.5)) > outline_thickness) break;
            float look_at_alpha;
            vec2 look_at_uv[8] = {
                modified_uv + vec2(x, y) * pixel_size,
                modified_uv + vec2(-x, y) * pixel_size,
                modified_uv + vec2(x, -y) * pixel_size,
                modified_uv + vec2(-x, -y) * pixel_size,
                modified_uv + vec2(y, x) * pixel_size,
                modified_uv + vec2(-y, x) * pixel_size,
                modified_uv + vec2(y, -x) * pixel_size,
                modified_uv + vec2(-y, -x) * pixel_size
            };
            for (int i = 0; i < 8; i++) {
                if (is_inside_usquare(look_at_uv[i])) {
                    // Check the original texture for outline detection
                    look_at_alpha = texture(tex, look_at_uv[i]).a;
                    if (look_at_alpha > alpha) alpha = look_at_alpha;
                    if (1.0 - alpha < 0.0001) break;
                }
            }

            if (1.0 - alpha < 0.0001) break;
        }

        if (1.0 - alpha < 0.0001) break;
    }

    // Return outline color if we found outline pixels, otherwise the flashed color
    if (alpha > 0.001) {
        // Apply damage flash to outline color too for consistency
        vec4 outline_with_alpha = vec4(outline_color.rgb, alpha * outline_color.a);
        return apply_damage_flash(outline_with_alpha);
    } else {
        return flashed_color;
    }
}

void vertex() {
	if (allow_out_of_bounds) VERTEX += (UV * 2.0 - 1.0) * outline_thickness;
}

void fragment() {
    COLOR = apply_outline(UV, TEXTURE, TEXTURE_PIXEL_SIZE);
}