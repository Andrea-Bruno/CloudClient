# Cloud Themes

This directory contains all available themes for the Cloud application.

## Available Themes

### 1. Modern Theme (`modern.css`)
- **Style**: Contemporary gradient design with smooth animations
- **Best For**: Modern applications, tech-savvy users
- **Features**: 
  - Gradient backgrounds
  - Smooth hover effects
  - Modern color palette (blue/purple)
  - Animated elements

### 2. Dark Theme (`dark.css`)
- **Style**: Dark mode optimized for low-light environments
- **Best For**: Night work, reduced eye strain
- **Features**:
  - Dark background (#0f0f23)
  - High contrast text
  - Optimized for OLED screens
  - Reduced blue light

### 3. Compact Theme (`compact.css`)
- **Style**: Dense layout with minimal spacing
- **Best For**: Power users, data-heavy applications
- **Features**:
  - Reduced padding and margins
  - Smaller font sizes
  - Maximum information density
  - Efficient use of screen space

### 4. Professional Theme (`professional.css`)
- **Style**: Clean corporate design
- **Best For**: Business environments, formal presentations
- **Features**:
  - Professional color scheme
  - Corporate styling
  - Print-optimized
  - Uppercase labels

## How to Use

### Configuration
Edit `appsettings.json`:

```json
{
  "Theme": "Modern"  // Options: "Modern", "Dark", "Compact", "Professional", or null for default
}
```

### Switching Themes
1. Open `appsettings.json`
2. Change the `"Theme"` value to one of the available themes
3. Restart the application
4. The new theme will be applied automatically

### Disable Themes
To use the default Bootstrap styling:
```json
{
  "Theme": null
}
```

## Creating Custom Themes

### File Structure
Create a new CSS file in this directory:
```
Cloud/wwwroot/themes/yourtheme.css
```

### Theme Template
```css
/* ===== Your Theme Name ===== */
:root {
    --your-primary: #yourcolor;
    /* Add more variables */
}

/* Override Bootstrap classes */
.alert { /* Your styles */ }
.btn { /* Your styles */ }
/* ... */
```

### Register Your Theme
1. Create the CSS file
2. Set in `appsettings.json`:
```json
{
  "Theme": "YourTheme"
}
```

## CSS Variables Reference

Each theme defines these standard variables:

```css
:root {
    --{theme}-primary: /* Primary color */
  --{theme}-secondary: /* Secondary color */
    --{theme}-success: /* Success color */
    --{theme}-danger: /* Danger color */
    --{theme}-warning: /* Warning color */
    --{theme}-info: /* Info color */
    --{theme}-shadow: /* Box shadow */
    --{theme}-transition: /* Transition timing */
}
```

## Bootstrap Classes Override

All themes override these Bootstrap classes:
- `.alert`, `.alert-*`
- `.btn`, `.btn-*`
- `.form-control`, `.form-select`, `.form-label`
- `.modal-*`
- `.card`, `.card-*`
- `.table`, `.table-*`
- Headings (`h1` - `h6`)

## Performance Notes

- Themes are pure CSS (no JavaScript)
- Minimal performance impact
- Cached by browser
- No additional HTTP requests after first load

## Browser Support

All themes support:
- Chrome/Edge (latest)
- Firefox (latest)
- Safari (latest)
- Mobile browsers

## Maintenance

### File Locations
```
Cloud/
??? wwwroot/
?   ??? themes/       ? All theme files here
?       ??? modern.css
?       ??? dark.css
?       ??? compact.css
?       ??? professional.css
?    ??? README.md     ? This file
??? Services/
    ??? ThemeService.cs   ? Theme service logic
```

### Removing a Theme
1. Delete the CSS file from `wwwroot/themes/`
2. Update `ThemeService.cs` `AvailableThemes` array if needed

### Modifying a Theme
1. Open the theme CSS file
2. Edit the CSS variables or styles
3. Save and refresh browser (Ctrl+F5)

## Troubleshooting

### Theme not loading?
- Check `appsettings.json` syntax
- Verify theme name matches filename (case-insensitive)
- Clear browser cache (Ctrl+F5)
- Check browser console for errors

### Styles not applying?
- Ensure Bootstrap is loaded before theme
- Check CSS specificity
- Use browser DevTools to inspect

### Creating conflicts?
- Avoid `!important` unless necessary
- Follow Bootstrap's naming conventions
- Test with all Bootstrap components

## Version History

- **v1.0.0** - Initial release with 4 themes
  - Modern
- Dark
  - Compact
  - Professional

## License

These themes are part of the Cloud application.

## Credits

- Designer: Cloud Theme Team
- Based on: Bootstrap 5.3
- Icons: Bootstrap Icons

## Support

For theme-related issues, check:
1. This README
2. Application logs
3. Browser console
4. `ThemeService.cs` implementation
