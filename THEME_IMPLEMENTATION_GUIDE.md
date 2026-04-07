# Theme System Implementation Guide

## 🎨 Overview

Your WPF application now supports **dynamic theme switching** between Light and Dark modes. Themes are:
- **Setting-driven** - User preference persisted to settings
- **Dynamic** - Switch themes without restarting the app
- **Centralized** - All theme colors defined in one place
- **Consistent** - Automatic update across all UI components

---

## 📁 Theme Structure

```
/Resources
    ├── Colors.xaml              # Theme loader (merges active theme)
    ├── Brushes.xaml             # Uses DynamicResource for theme switching
    ├── Themes/
    │   ├── Dark.xaml           # Dark theme color definitions
    │   └── Light.xaml          # Light theme color definitions
    
/Services
    └── ThemeManager.cs          # Theme switching service

/Properties
    └── Settings.settings        # Persisted theme preference
```

---

## 🔑 Key Concepts

### 1. **DynamicResource vs StaticResource**

**IMPORTANT:** For theme switching to work, all color/brush references MUST use `DynamicResource`:

```xml
<!-- ✅ CORRECT - Will update when theme changes -->
<TextBlock Foreground="{DynamicResource Brush.TextPrimary}"/>
<Border Background="{DynamicResource Brush.Surface}"/>

<!-- ❌ WRONG - Will NOT update when theme changes -->
<TextBlock Foreground="{StaticResource Brush.TextPrimary}"/>
<Border Background="{StaticResource Brush.Surface}"/>
```

**Current Status:**
- ✅ `Brushes.xaml` - Uses `DynamicResource` (ready for themes)
- ✅ All component styles use brushes (indirect theme support)
- ⚠️ Need to verify XAML views use component styles (not direct colors)

### 2. **Theme Color Definitions**

Each theme defines the same color keys with different values:

#### Dark Theme (`Themes/Dark.xaml`)
```xml
<Color x:Key="Color.Background">#0F0F0F</Color>   <!-- Nearly black -->
<Color x:Key="Color.TextPrimary">#FFFFFF</Color>  <!-- White -->
```

#### Light Theme (`Themes/Light.xaml`)
```xml
<Color x:Key="Color.Background">#FFFFFF</Color>   <!-- White -->
<Color x:Key="Color.TextPrimary">#212121</Color>  <!-- Dark gray -->
```

### 3. **Brand Colors**

Some colors remain the same across themes (brand identity):
- `Color.Primary` (#E2156B - Pink)
- `Color.Secondary` (#1AB4CC - Teal)
- `Color.Success`, `Color.Warning`, `Color.Error` (State colors)

---

## 🚀 Using ThemeManager

### Initialize on App Startup

In `App.xaml.cs` or `MainWindow.xaml.cs` constructor:

```csharp
using x_phy_wpf_ui.Services;

public App()
{
    InitializeComponent();
    
    // Load saved theme preference (call AFTER InitializeComponent)
    ThemeManager.LoadSavedTheme();
}
```

### Switch Themes

```csharp
using x_phy_wpf_ui.Services;

// Apply specific theme
ThemeManager.ApplyTheme(ThemeManager.Theme.Dark);
ThemeManager.ApplyTheme(ThemeManager.Theme.Light);

// Toggle between themes
ThemeManager.ToggleTheme();

// Get current theme
var currentTheme = ThemeManager.CurrentTheme; // Returns Theme.Dark or Theme.Light
```

### Add Theme Toggle Button (Example)

In your settings UI or navigation bar:

```xml
<!-- XAML -->
<Button Content="Toggle Theme" 
        Style="{StaticResource Button.Secondary}"
        Click="ToggleTheme_Click"/>
```

```csharp
// Code-behind
private void ToggleTheme_Click(object sender, RoutedEventArgs e)
{
    ThemeManager.ToggleTheme();
}
```

Or with icon/text display:

```xml
<Button Click="ToggleTheme_Click" Style="{StaticResource Button.Icon}">
    <StackPanel Orientation="Horizontal">
        <TextBlock x:Name="ThemeIcon" Text="🌙" FontSize="16" Margin="{StaticResource Spacing.XS}"/>
        <TextBlock x:Name="ThemeText" Text="Dark Mode" Style="{StaticResource Text.Body}"/>
    </StackPanel>
</Button>
```

```csharp
private void ToggleTheme_Click(object sender, RoutedEventArgs e)
{
    ThemeManager.ToggleTheme();
    
    // Update button display
    if (ThemeManager.CurrentTheme == ThemeManager.Theme.Light)
    {
        ThemeIcon.Text = "☀️";
        ThemeText.Text = "Light Mode";
    }
    else
    {
        ThemeIcon.Text = "🌙";
        ThemeText.Text = "Dark Mode";
    }
}
```

---

## ✅ Refactoring Checklist for Theme Support

When refactoring each XAML file:

### 1. **Use Component Styles (Preferred)**

Component styles automatically support themes:

```xml
<!-- ✅ CORRECT - Uses component style (theme-aware) -->
<TextBlock Text="Title" Style="{StaticResource Text.Title}"/>
<Button Style="{StaticResource Button.Primary}"/>
<Border Style="{StaticResource Border.InputContainer}"/>
```

### 2. **When You Need Direct Color References**

If you must reference colors directly (rare), use `DynamicResource`:

```xml
<!-- ✅ CORRECT - Will update with theme -->
<Rectangle Fill="{DynamicResource Brush.Surface}"/>
<Path Fill="{DynamicResource Brush.TextSecondary}"/>

<!-- ❌ WRONG - Won't update with theme -->
<Rectangle Fill="{StaticResource Brush.Surface}"/>
```

### 3. **Avoid Hardcoded Colors**

```xml
<!-- ❌ BAD - Will never change with theme -->
<Border Background="#1E1E1E"/>
<TextBlock Foreground="#FFFFFF"/>

<!-- ✅ GOOD - Theme-aware -->
<Border Background="{DynamicResource Brush.Surface}"/>
<TextBlock Foreground="{DynamicResource Brush.TextPrimary}"/>

<!-- ✅ BEST - Use component style -->
<Border Style="{StaticResource Border.InputContainer}"/>
<TextBlock Style="{StaticResource Text.Body}"/>
```

---

## 🎨 Light Theme Color Palette

For reference when testing:

| Element | Dark Theme | Light Theme |
|---------|-----------|-------------|
| Background | `#0F0F0F` (Nearly black) | `#FFFFFF` (White) |
| Surface | `#1E1E1E` (Dark gray) | `#F5F5F5` (Light gray) |
| Card | `#1A1A1A` (Dark) | `#FAFAFA` (Off-white) |
| Border | `#2A2A2A` (Medium gray) | `#E0E0E0` (Light gray) |
| Text Primary | `#FFFFFF` (White) | `#212121` (Dark gray) |
| Text Secondary | `#B0B0B0` (Gray) | `#757575` (Medium gray) |
| Disabled | `#555555` (Gray) | `#BDBDBD` (Light gray) |

**Brand Colors (Same in both themes):**
- Primary: `#E2156B` (Pink)
- Secondary: `#1AB4CC` (Teal)
- Success: `#4CAF50` (Green)
- Warning: `#FFA726` (Orange)
- Error: `#FF6B6B` (Red)

---

## 🧪 Testing Themes

### Manual Testing Steps

1. **Launch Application**
   - Default theme should be Dark (or last saved preference)

2. **Switch to Light Theme**
   ```csharp
   ThemeManager.ApplyTheme(ThemeManager.Theme.Light);
   ```
   - Verify all text is readable (dark on light)
   - Verify borders are visible
   - Verify buttons have proper contrast

3. **Switch to Dark Theme**
   ```csharp
   ThemeManager.ApplyTheme(ThemeManager.Theme.Dark);
   ```
   - Verify all text is readable (light on dark)
   - Verify UI matches original design

4. **Toggle Multiple Times**
   - Ensure no visual glitches
   - Verify persistence (restart app, theme should be remembered)

### Components to Test

Test theme switching on:
- [ ] Sign In screen
- [ ] Create Account screen
- [ ] Password reset flows
- [ ] Main dashboard/detection screen
- [ ] Results display
- [ ] Settings screen
- [ ] All buttons (Primary, Secondary, Link)
- [ ] All input fields
- [ ] Navigation bars
- [ ] Modal dialogs

---

## 🔧 Troubleshooting

### Theme Doesn't Switch

**Problem:** Colors don't update when switching themes.

**Solutions:**
1. Ensure `Brushes.xaml` uses `DynamicResource` (✅ already done)
2. Verify XAML views use brush resources, not hardcoded colors
3. Check `ThemeManager.ApplyTheme()` is called correctly
4. Verify `Colors.xaml` merges theme dictionaries

### Some Elements Don't Update

**Problem:** Most UI updates but some elements stay the same.

**Solutions:**
1. Check if element uses `StaticResource` (change to `DynamicResource`)
2. Verify element uses design system brushes
3. Check for hardcoded colors in XAML

### Theme Preference Not Saved

**Problem:** Theme resets to Dark on restart.

**Solutions:**
1. Verify `Settings.settings` file exists
2. Check `SaveThemePreference()` is not throwing exceptions
3. Ensure app has write permissions to settings file

---

## 📋 Implementation Status

### ✅ Complete
- Theme structure created (Dark.xaml, Light.xaml)
- ThemeManager service implemented
- Settings integration for persistence
- Brushes updated to use DynamicResource
- Light theme colors defined

### 🔄 In Progress (Hybrid Refactoring)
- Refactoring XAML files to use component styles
- Ensuring all views use design system (theme-aware)

### ⏳ To Do
- Add theme toggle UI in settings/navigation
- Initialize ThemeManager in App startup
- Test all components in both themes
- Add theme switching animation (optional)
- Document theme customization for future colors

---

## 🎯 Quick Start

### Step 1: Initialize on Startup

```csharp
// App.xaml.cs
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Load saved theme
        ThemeManager.LoadSavedTheme();
    }
}
```

### Step 2: Add Toggle Button

Add to your settings screen or top navigation:

```xml
<Button Content="🌙 Toggle Theme" 
        Style="{StaticResource Button.Ghost}"
        Click="ToggleTheme_Click"/>
```

```csharp
private void ToggleTheme_Click(object sender, RoutedEventArgs e)
{
    ThemeManager.ToggleTheme();
}
```

### Step 3: Test

1. Launch app (should load saved theme or default to Dark)
2. Click toggle button
3. Verify entire UI updates instantly
4. Restart app
5. Verify theme is remembered

---

## 💡 Best Practices

1. **Always use component styles** - They're theme-aware by default
2. **Test both themes** - Ensure contrast and readability in both
3. **Use semantic color names** - `Brush.Surface` not `Brush.DarkGray`
4. **Document custom colors** - If adding new theme colors, document them
5. **Consider accessibility** - Ensure sufficient contrast in both themes

---

## 📚 Related Files

- **Theme Definitions:** `Resources/Themes/Dark.xaml`, `Light.xaml`
- **Theme Manager:** `Services/ThemeManager.cs`
- **Settings:** `Properties/Settings.settings`
- **Design System:** `Resources/Colors.xaml`, `Brushes.xaml`
- **Migration Guide:** `DESIGN_SYSTEM_MIGRATION.md`

---

**Theme system is ready!** All new/refactored XAML files will automatically support theme switching when using the design system component styles. 🎨
