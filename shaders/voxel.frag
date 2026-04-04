#version 330 core
out vec4 FragColor;

flat in uint vType;
flat in float vGrowth;
flat in float vMoisture;
in float vWorldY;

uniform float uAlteredState; // 0.0 to 1.0

// Helper to rotate hue while preserving value/saturation
vec3 hueShift(vec3 color, float hue) {
    const vec3 k = vec3(0.57735);
    float cosAngle = cos(hue);
    return color * cosAngle + cross(k, color) * sin(hue) + k * dot(k, color) * (1.0 - cosAngle);
}

void main() {
    // Base color determined by Voxel Type
    vec3 baseColor;

    // Debug: Force white for everything below the water line (14.0)
    
    if (vType == 1u) baseColor = vec3(0.2, 0.6, 0.2);      // Grass
    else if (vType == 2u) baseColor = vec3(0.6, 0.4, 0.2); // Dirt/Soil
    else if (vType == 3u) baseColor = vec3(0.0, 0.3, 0.8); // Water
    else if (vType == 4u) baseColor = vec3(0.9, 0.8, 0.5); // Sand
    else if (vType == 5u) baseColor = vec3(0.92, 0.95, 1.0); // Snow
    else baseColor = vec3(1.0, 1.0, 1.0);
    

    // Visualize Growth (Brightness) and Moisture (Blue Tint)
    vec3 normalColor = baseColor * (0.5 + 0.5 * vGrowth); 
    normalColor = mix(normalColor, vec3(0.0, 0.0, 1.0), vMoisture * 0.5);

    // Debug: Shift the existing data along the color spectrum
    vec3 alteredColor = hueShift(normalColor, uAlteredState * 3.14159);
    
    // Linear interpolation between palettes
    vec3 finalColor = mix(normalColor, alteredColor, uAlteredState);
    
    // Add a subtle 'pulse' effect if in altered state
    if(uAlteredState > 0.1) {
        finalColor *= 0.8 + 0.2 * sin(uAlteredState * 5.0);
    }
    
    // Set Alpha: 0.6 for Water, 1.0 for everything else
    float alpha = (vType == 3u) ? 0.6 : 1.0;
    
    FragColor = vec4(finalColor, alpha);
}
