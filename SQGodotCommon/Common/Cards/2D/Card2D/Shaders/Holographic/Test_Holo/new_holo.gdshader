shader_type canvas_item;
render_mode blend_mix;

// ===== HOLOGRAPHIC EFFECT CONTROLS =====
group_uniforms holographic;
uniform bool enable_holographic = true;
uniform float holographic_intensity : hint_range(0.0, 2.0, 0.01) = 1.0;
uniform sampler2D rainbow_texture : hint_default_white, filter_linear_mipmap;
uniform float rainbow_shift : hint_range(-1.0, 1.0, 0.01) = 0.0;
uniform float rainbow_speed : hint_range(0.0, 5.0, 0.1) = 1.0;

// ===== TWIRL DISTORTION CONTROLS =====
group_uniforms twirl_distortion;
uniform bool enable_twirl = true;
uniform float twirl_strength : hint_range(0.0, 10.0, 0.1) = 4.0;
uniform vec2 twirl_center = vec2(0.5, 0.5);
uniform float twirl_speed : hint_range(0.0, 20.0, 0.1) = 10.0;
uniform float twirl_frequency : hint_range(0.0, 5.0, 0.1) = 0.1;

// ===== FREQUENCY MODULATION CONTROLS =====
group_uniforms frequency_modulation;
uniform bool enable_frequency_mod = true;
uniform float frequency_strength : hint_range(0.0, 10.0, 0.1) = 1.0;
uniform int frequency_axis : hint_range(0, 2) = 0; // 0=X, 1=Y, 2=Both
uniform float frequency_offset : hint_range(0.0, 6.28, 0.01) = 0.0;

// ===== NOISE MODULATION CONTROLS =====
group_uniforms noise_modulation;
uniform bool enable_noise = true;
uniform sampler2D noise_texture : hint_default_white, filter_linear_mipmap;
uniform vec2 noise_tiling = vec2(1.0, 1.0);
uniform vec2 noise_speed  = vec2(0.1, 0.0);
uniform float noise_power : hint_range(0.1, 10.0, 0.1) = 5.0;
uniform float noise_intensity : hint_range(0.0, 2.0, 0.01) = 1.0;
uniform bool invert_noise = false;

// ===== ANIMATION CONTROLS =====
group_uniforms animation;
uniform float global_time_scale : hint_range(0.0, 5.0, 0.1) = 1.0;
uniform bool pause_animation = false;
uniform float manual_time : hint_range(0.0, 100.0, 0.1) = 0.0;

// ===== BLEND AND OUTPUT CONTROLS =====
group_uniforms blending;
uniform int blend_mode : hint_range(0, 3) = 0; // 0=Add, 1=Multiply, 2=Overlay, 3=Screen
uniform float effect_opacity : hint_range(0.0, 1.0, 0.01) = 1.0;
uniform bool preserve_alpha = true;
uniform vec3 color_tint : source_color = vec3(1.0, 1.0, 1.0);

// ===== UTILITY FUNCTIONS =====

// Enhanced twirl function with falloff
vec2 twirl_uv(vec2 uv, vec2 center, float strength) {
    vec2 delta = uv - center;
    float distance = length(delta);
    
    // Add distance-based falloff for more natural twirl
    float falloff = 1.0 - smoothstep(0.0, 0.7, distance);
    float angle = strength * falloff;
    
    float cos_angle = cos(angle);
    float sin_angle = sin(angle);
    
    mat2 rotation = mat2(vec2(cos_angle, -sin_angle), vec2(sin_angle, cos_angle));
    vec2 rotated_delta = rotation * delta;
    
    return center + rotated_delta;
}

// Improved tiling and offset with seamless wrapping
vec2 tiling_and_offset(vec2 uv, vec2 tiling, vec2 offset) {
    return fract(uv * tiling + offset);
}

// Color blending functions
vec3 blend_add(vec3 base, vec3 overlay) {
    return base + overlay;
}

vec3 blend_multiply(vec3 base, vec3 overlay) {
    return base * overlay;
}

vec3 blend_overlay(vec3 base, vec3 overlay) {
    return mix(2.0 * base * overlay, 1.0 - 2.0 * (1.0 - base) * (1.0 - overlay), step(0.5, base));
}

vec3 blend_screen(vec3 base, vec3 overlay) {
    return 1.0 - (1.0 - base) * (1.0 - overlay);
}

// Enhanced noise sampling with better distribution
float sample_noise(sampler2D tex, vec2 uv, float power, bool invert_noise2) {
    // Use smoothed UV coordinates to reduce aliasing
    vec2 smooth_uv = fract(uv);
    vec4 noise_sample = textureLod(tex, uv, 0.0);
    
    // Use just the red channel or luminance for cleaner noise
    float noise_value = noise_sample.r;
    
    if (invert_noise2) {
        noise_value = 1.0 - noise_value;
    }
    
    // Smooth the power curve to avoid harsh transitions
    noise_value = smoothstep(0.0, 1.0, noise_value);    	
	return pow(max(noise_value, 0.001), power);
}


// Frequency modulation with multiple axis support
// Frequency modulation with multiple axis support
float calculate_frequency_modulation(vec2 screen_uv, float strength, int axis, float offset) {
    float freq_input = screen_uv.x; // Default to X axis
    
    if (axis == 1) {
        freq_input = screen_uv.y;
    } else if (axis == 2) {
        freq_input = (screen_uv.x + screen_uv.y) * 0.5;
    }
    
    return sin(freq_input * strength + offset);
}

void fragment() {
    // Calculate effective time
    float effective_time = pause_animation ? manual_time : TIME * global_time_scale;
    
    // Get base texture
    vec4 base_texture = texture(TEXTURE, UV);
    vec3 final_color = base_texture.rgb;
    
    // Check if any effects are enabled
    bool any_effects_enabled = enable_holographic || enable_twirl || enable_noise;
    
    // Only process effects if any are enabled
    if (any_effects_enabled) {
        // ===== TWIRL DISTORTION =====
        vec2 working_uv = UV;
        if (enable_twirl) {
            float twirl_time = effective_time * twirl_speed * twirl_frequency;
            float animated_strength = twirl_strength * sin(twirl_time);
            working_uv = twirl_uv(UV, twirl_center, animated_strength);
        }
        
        // ===== FREQUENCY MODULATION =====
        float frequency_mod = 1.0;
        if (enable_frequency_mod) {
            frequency_mod = calculate_frequency_modulation(SCREEN_UV, frequency_strength, frequency_axis, frequency_offset);
        }
        
        // ===== HOLOGRAPHIC EFFECT =====
        vec3 holographic_contribution = vec3(0.0);
        if (enable_holographic) {
            // Animate rainbow sampling
            float rainbow_time = effective_time * rainbow_speed;
            vec2 rainbow_uv = tiling_and_offset(working_uv, vec2(frequency_mod), vec2(rainbow_shift + rainbow_time * 0.1));
            
            vec4 rainbow_sample = texture(rainbow_texture, rainbow_uv);
            holographic_contribution = rainbow_sample.rgb * holographic_intensity;
        }
        
        // ===== NOISE MODULATION =====
        float noise_multiplier = 1.0;
        if (enable_noise) {
            vec2 noise_time = effective_time * noise_speed;
            vec2 noise_uv = tiling_and_offset(UV, noise_tiling, noise_time);

            //noise_uv = mod(noise_uv, vec2(1.0));
            
            noise_multiplier = sample_noise(noise_texture, noise_uv, noise_power, invert_noise);
            noise_multiplier *= noise_intensity;
        }
        
        // ===== COMBINE EFFECTS =====
        vec3 effect_color = holographic_contribution * noise_multiplier;
        effect_color *= color_tint;
        
        // Apply blending mode
        if (blend_mode == 0) { // Add
            final_color = blend_add(final_color, effect_color);
        } else if (blend_mode == 1) { // Multiply
            final_color = blend_multiply(final_color, effect_color);
        } else if (blend_mode == 2) { // Overlay
            final_color = blend_overlay(final_color, effect_color);
        } else if (blend_mode == 3) { // Screen
            final_color = blend_screen(final_color, effect_color);
        }
        
        // Apply effect opacity
        final_color = mix(base_texture.rgb, final_color, effect_opacity);
    }
    
    // Output final color
    COLOR.rgb = final_color;
    COLOR.a = preserve_alpha ? base_texture.a : mix(base_texture.a, 1.0, effect_opacity);
}