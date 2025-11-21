# ZPlayer

A video player for VRChat, made for my personal use, with focus on high video quality

# Requires UI+ Shader
- https://z3y.booth.pm/items/7637247

# Features

- Modern SDF UI with liquid glass and rounded corners
- Disable Post Processing toggle
- No black bars for non 16:9 videos
  - Display scales with the aspect ratio
- Super Sampling for a sharper image
- Exact gamma color conversion for AVPro
- No resampling
  - AVPro texture gets copied to a render texture with the same resolution
  - The pixel grid is perfectly aligned so the pixels don't get stretched
- Logarithmic volume slider
- Lock for object owner and instance master
- AVPro Only (does not work in Editor)

# Creator Companion Listing:
```
https://z3y.github.io/vpm-package-listing/
```

![Preview](/Image~/VRChat_2025-11-21_21-59-28.961_3840x2160.png)
