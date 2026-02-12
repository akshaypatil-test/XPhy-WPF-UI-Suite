# WPF Design System Migration Guide

## Overview

This project has been refactored to use a centralized enterprise design system that enforces visual consistency and maintainability across all XAML views.

## What Changed

### 1. **New Centralized Resource Structure**

All design tokens and component styles have been moved to:

```
/Resources
    ├── Colors.xaml           # Color definitions
    ├── Brushes.xaml          # Brush resources
    ├── Typography.xaml       # Text styles
    ├── Spacing.xaml          # Spacing tokens
    ├── Radius.xaml           # Corner radius tokens
    └── Styles/
        ├── Buttons.xaml      # Button component styles
        ├── Inputs.xaml       # Input component styles
        ├── Cards.xaml        # Card/container styles
        └── Lists.xaml        # List/grid styles
```

### 2. **App.xaml Updated**

`App.xaml` now merges all centralized ResourceDictionaries and provides backward compatibility aliases for legacy key names.

### 3. **Files Refactored**

**Completed:**
- ✅ `Controls/SignInComponent.xaml` - Fully migrated to design system
- ✅ `Controls/UpdatePasswordComponent.xaml` - Fully migrated to design system
- ✅ `App.xaml` - Updated to merge all resource dictionaries

**Pending Refactoring:**
- All other XAML files in `/Controls` still contain inline styles and hardcoded values

## Design Tokens Reference

### Colors
- `Color.Primary` (#E2156B)
- `Color.Secondary` (#1AB4CC)
- `Color.TextPrimary` (#FFFFFF)
- `Color.TextSecondary` (#B0B0B0)
- `Color.Error` (#FF6B6B)
- `Color.Surface` (#1E1E1E)
- `Color.Background` (#0F0F0F)

### Brushes
- `Brush.Primary`, `Brush.PrimaryHover`
- `Brush.Secondary`, `Brush.SecondaryHover`
- `Brush.TextPrimary`, `Brush.TextSecondary`
- `Brush.Error`
- `Brush.Surface`, `Brush.Background`
- `Brush.Border`, `Brush.BorderFocused`

### Typography Styles
- `Text.Title` - 24px Bold
- `Text.Subtitle` - 16px SemiBold
- `Text.Body` - 14px Normal
- `Text.BodySecondary` - 14px Normal (secondary color)
- `Text.Caption` - 12px Normal (secondary color)
- `Text.CaptionBold` - 12px Bold (secondary color)
- `Text.Error` - 11px (error color)
- `Text.Button` - 16px SemiBold

### Spacing Tokens
- `Spacing.XS` (4px)
- `Spacing.S` (8px)
- `Spacing.M` (12px)
- `Spacing.L` (16px)
- `Spacing.XL` (24px)
- `Spacing.XXL` (32px)

Common patterns:
- `Padding.Button` (24,10)
- `Padding.Input` (16,8)
- `Margin.FieldBottom` (0,0,0,4)
- `Margin.FieldGroup` (0,0,0,8)
- `Margin.SectionBottom` (0,0,0,16)

### Corner Radius
- `Radius.S` (4px)
- `Radius.M` (8px)
- `Radius.L` (12px)

## Component Style Keys

### Buttons
- `Button.Primary` - Primary pink button
- `Button.Secondary` - Outlined secondary button
- `Button.Back` - Teal back button
- `Button.Ghost` - Transparent ghost button
- `Button.Link` - Link-style button
- `Button.Danger` - Red danger button
- `Button.Icon` - Icon-only button

### Inputs
- `TextBox.Standard` - Standard text input
- `TextBox.Inner` - Inner textbox (for composite controls)
- `TextBox.InnerWithIcon` - Inner textbox with icon padding
- `PasswordBox.Standard` - Standard password input
- `PasswordBox.Inner` - Inner password box (for composite controls)
- `CheckBox.Standard` - Standard checkbox
- `ComboBox.Standard` - Standard combobox
- `DatePicker.Standard` - Standard date picker
- `Label.Field` - Field label style
- `Border.InputContainer` - Input container border
- `TextBlock.Placeholder` - Placeholder text style

### Cards & Containers
- `Card.Standard` - Standard card
- `Card.Transparent` - Transparent card
- `Panel.Surface` - Surface panel

### Lists
- `ListView.Standard` - Standard list view
- `DataGrid.Standard` - Standard data grid

## Migration Examples

### Before (Inline Styles):
```xaml
<TextBlock Text="Sign In" 
           FontSize="24" 
           FontWeight="Bold" 
           Foreground="#FFFFFF"
           Margin="0,0,0,8"/>

<Button Background="#E2156B"
        Foreground="White"
        FontSize="16"
        Padding="40,14"
        CornerRadius="8"/>
```

### After (Design System):
```xaml
<TextBlock Text="Sign In" 
           Style="{StaticResource Text.Title}"
           Margin="{StaticResource Margin.FieldGroup}"/>

<Button Style="{StaticResource Button.Primary}"
        Content="Sign In"/>
```

## How to Refactor Remaining Files

### Step 1: Remove Local Resources
Replace this:
```xaml
<UserControl.Resources>
    <SolidColorBrush x:Key="PrimaryPink" Color="#E2156B"/>
    <!-- ... other local resources ... -->
</UserControl.Resources>
```

With this:
```xaml
<UserControl.Resources>
    <!-- No local resources needed - using centralized design system -->
</UserControl.Resources>
```

### Step 2: Replace Inline Typography
- `FontSize="24" FontWeight="Bold" Foreground="#FFFFFF"` → `Style="{StaticResource Text.Title}"`
- `FontSize="14" Foreground="#B0B0B0"` → `Style="{StaticResource Text.BodySecondary}"`
- `FontSize="12" FontWeight="Bold" Foreground="#B0B0B0"` → `Style="{StaticResource Text.CaptionBold}"`

### Step 3: Replace Hardcoded Margins
- `Margin="0,0,0,8"` → `Margin="{StaticResource Margin.FieldGroup}"`
- `Margin="0,0,0,32"` → `Margin="{StaticResource Margin.ContentBottom}"`
- `Margin="0,0,0,16"` → `Margin="{StaticResource Margin.SectionBottom}"`

### Step 4: Use Component Styles
- Custom button styles → `Style="{StaticResource Button.Primary}"` or `Button.Secondary`
- Custom textbox styles → `Style="{StaticResource TextBox.Standard}"`
- Custom password styles → `Style="{StaticResource PasswordBox.Standard}"`

### Step 5: Replace Input Containers
For composite input controls with borders:
```xaml
<Border Background="{StaticResource Brush.InputBackground}"
        BorderBrush="{StaticResource Brush.Border}"
        BorderThickness="1"
        CornerRadius="{StaticResource Radius.M}"
        MinHeight="{StaticResource MinHeight.Input}">
```

Use:
```xaml
<Border Style="{StaticResource Border.InputContainer}">
```

## Backward Compatibility

Legacy resource keys are maintained in `App.xaml` for backward compatibility:
- `TextPrimary` (legacy) → `Brush.TextPrimary` (new)
- `PrimaryPink` (legacy) → `Brush.Primary` (new)
- `PrimaryButtonStyle` (legacy) → `Button.Primary` (new)

This ensures existing code continues to work during gradual migration.

## Benefits

1. **Consistency** - All UI elements follow the same visual language
2. **Maintainability** - Change a color/font once, updates everywhere
3. **Scalability** - Easy to add new components following the same patterns
4. **Accessibility** - Centralized focus/hover/disabled states
5. **Theming** - Easy to implement dark/light themes by swapping resource dictionaries

## Next Steps

1. Continue refactoring remaining XAML files in `/Controls`
2. Remove all duplicate local resource definitions
3. Test each component after refactoring to ensure no visual regressions
4. Update any custom controls to use the design system
5. Consider adding theme variants (Light/Dark) as separate ResourceDictionaries

## Testing Checklist

After refactoring each file:
- [ ] No visual regressions (colors, fonts, spacing match original)
- [ ] All interactions work (hover, focus, click)
- [ ] Validation error styles display correctly
- [ ] Disabled states render properly
- [ ] No XAML compilation errors
- [ ] Application runs without crashes

## Support

For questions or issues with the design system:
- Refer to style definitions in `/Resources/Styles/*.xaml`
- Check token values in `/Resources/*.xaml`
- Review refactored examples in `SignInComponent.xaml` and `UpdatePasswordComponent.xaml`
