# Hybrid Refactoring Guide - Your Review Tasks

## 🎯 Hybrid Approach Overview

**AI Part (Done):** Clean up Resources sections, remove duplicate styles  
**Your Part (Review Required):** Update content to use design system styles  

---

## ✅ Files AI Has Cleaned (Resources Removed)

### Fully Refactored (Content + Resources)
1. ✅ **SignInComponent.xaml** - 100% complete, ready for use
2. ✅ **UpdatePasswordComponent.xaml** - 100% complete, ready for use

### Resources Cleaned (Content Needs Review)
3. 🔄 **CreateAccountComponent.xaml** - Resources removed, content needs style updates

---

## 📝 What You Need to Do

For each file with resources cleaned, you need to update the **content section** to use design system styles. Here's the systematic approach:

### Step 1: Open the File
Open `CreateAccountComponent.xaml` (or next file in queue)

### Step 2: Find & Replace Pattern

Use these find/replace patterns in your editor:

#### Typography Updates

| Find This | Replace With |
|-----------|--------------|
| `FontSize="24" FontWeight="Bold" Foreground="{StaticResource TextPrimary}"` | `Style="{StaticResource Text.Title}"` |
| `FontSize="14" Foreground="{StaticResource TextSecondary}"` | `Style="{StaticResource Text.BodySecondary}"` |
| `FontSize="12" FontWeight="Bold" Foreground="{StaticResource TextSecondary}"` | `Style="{StaticResource Text.CaptionBold}"` |
| `FontSize="11" Foreground="{StaticResource ErrorText}"` or `Foreground="#FF6B6B"` | `Style="{StaticResource Text.Error}"` |

#### Spacing Updates

| Find This | Replace With |
|-----------|--------------|
| `Margin="0,0,0,8"` | `Margin="{StaticResource Margin.FieldGroup}"` |
| `Margin="0,0,0,32"` | `Margin="{StaticResource Margin.ContentBottom}"` |
| `Margin="0,0,0,16"` | `Margin="{StaticResource Margin.SectionBottom}"` |
| `Margin="0,0,0,4"` | `Margin="{StaticResource Margin.FieldBottom}"` |

#### Style Key Updates

| Find This (Old Style Keys) | Replace With (New Design System) |
|-----------|--------------|
| `Style="{StaticResource ModernTextBoxInnerStyleFullWidth}"` | `Style="{StaticResource TextBox.Inner}"` |
| `Style="{StaticResource ModernTextBoxInnerStyle}"` | `Style="{StaticResource TextBox.InnerWithIcon}"` |
| `Style="{StaticResource ModernPasswordBoxInnerStyle}"` | `Style="{StaticResource PasswordBox.Inner}"` |
| `Style="{StaticResource LaunchButtonStyle}"` | `Style="{StaticResource Button.Primary}"` |
| `Style="{StaticResource BackButtonStyle}"` | `Style="{StaticResource Button.Back}"` |
| `Style="{StaticResource LinkButtonStyle}"` | `Style="{StaticResource Button.Link}"` |

#### Input Container Updates

Find blocks like this:
```xml
<Border Background="{StaticResource InputBackground}"
        BorderBrush="{StaticResource InputBorder}"
        BorderThickness="1"
        CornerRadius="8"
        MinHeight="44"
        Margin="0,0,0,4">
```

Replace with:
```xml
<Border Style="{StaticResource Border.InputContainer}"
        Margin="{StaticResource Margin.FieldBottom}">
```

### Step 3: Test the File

After making changes:
1. Build the project (`Ctrl+Shift+B` or `dotnet build`)
2. Fix any compilation errors
3. Run the application
4. Navigate to the updated component
5. Verify visual appearance matches original
6. Test all interactions (buttons, inputs, validation)

### Step 4: Test Theme Switching

```csharp
// In any code-behind or debug immediate window
ThemeManager.ToggleTheme();
```

Verify the component looks good in both Light and Dark themes.

---

## 📋 File Queue for Review

Process these files in order (high priority first):

### Phase 1: Critical Auth Flow (Do These First)

#### File: CreateAccountComponent.xaml
- **Status:** Resources cleaned ✅
- **Lines:** ~512
- **Task:** Update content to use design system styles
- **Priority:** HIGH
- **Estimated Time:** 20-30 minutes

#### File: ForgotPasswordComponent.xaml  
- **Status:** Pending resources cleanup
- **Lines:** ~173
- **Task:** Clean resources, then update content
- **Priority:** HIGH
- **Estimated Time:** 15-20 minutes

#### File: ResetPasswordComponent.xaml
- **Status:** Pending resources cleanup
- **Lines:** ~220
- **Task:** Clean resources, then update content
- **Priority:** HIGH
- **Estimated Time:** 15-20 minutes

#### File: ForgotPasswordVerifyOtpComponent.xaml
- **Status:** Pending resources cleanup
- **Lines:** ~180
- **Task:** Clean resources, then update content
- **Priority:** HIGH
- **Estimated Time:** 15-20 minutes

#### File: EmailVerificationComponent.xaml
- **Status:** Pending resources cleanup
- **Lines:** ~170
- **Task:** Clean resources, then update content
- **Priority:** HIGH
- **Estimated Time:** 15-20 minutes

#### File: RecoverUsernameComponent.xaml
- **Status:** Pending resources cleanup
- **Lines:** ~165
- **Task:** Clean resources, then update content
- **Priority:** HIGH
- **Estimated Time:** 15-20 minutes

### Phase 2: UI Components

7. LaunchComponent.xaml
8. TopNavigationBar.xaml
9. BottomBar.xaml
10. SupportComponent.xaml

### Phase 3: Feature Components

11. DetectionResultsScreen.xaml (largest: 328 lines)
12. DetectionResultsComponent.xaml
13. SessionDetailsPanel.xaml
... (others)

---

## 🛠️ Tools to Help You

### Visual Studio / VS Code Extensions

1. **Find & Replace with Regex**
   - In VS: `Ctrl+H`, enable regex mode
   - Can do bulk replacements

2. **Multi-Cursor Editing**
   - Select multiple instances
   - Edit all at once

3. **XML Formatting**
   - After changes, auto-format: `Ctrl+K, Ctrl+D`

### PowerShell Script (Optional)

If you want to automate some replacements:

```powershell
# Example: Replace all ModernTextBoxInnerStyleFullWidth
$files = Get-ChildItem "Controls\*.xaml" -Recurse
foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $content = $content -replace 'Style="\{StaticResource ModernTextBoxInnerStyleFullWidth\}"', 'Style="{StaticResource TextBox.Inner}"'
    Set-Content $file.FullName $content
}
```

---

## ✅ Verification Checklist

After refactoring each file:

### Visual Check
- [ ] No visual regressions (looks identical to before)
- [ ] Colors match
- [ ] Spacing/margins match
- [ ] Border radius matches
- [ ] Fonts are correct size/weight

### Interaction Check
- [ ] Buttons work (hover, click, disabled)
- [ ] Inputs work (typing, focus, validation)
- [ ] Links/navigation work
- [ ] Error messages display correctly

### Theme Check
- [ ] Looks good in Dark theme
- [ ] Looks good in Light theme
- [ ] All text is readable in both themes
- [ ] Borders visible in both themes
- [ ] Proper contrast in both themes

### Technical Check
- [ ] No XAML compilation errors
- [ ] No runtime exceptions
- [ ] Resource keys resolve correctly
- [ ] Data binding still works

---

## 🎨 Theme Testing Command

Add this temporary button to any view for quick testing:

```xml
<Button Content="Toggle Theme" 
        Style="{StaticResource Button.Secondary}"
        Click="TestTheme_Click"
        VerticalAlignment="Bottom"
        HorizontalAlignment="Right"
        Margin="{StaticResource Spacing.L}"/>
```

```csharp
private void TestTheme_Click(object sender, RoutedEventArgs e)
{
    ThemeManager.ToggleTheme();
}
```

---

## 💡 Tips & Best Practices

### Tip 1: Work in Small Batches
Don't try to do all files at once. Do 2-3 files, test thoroughly, then continue.

### Tip 2: Use Version Control
Commit after each file is successfully refactored and tested.

### Tip 3: Keep Reference Open
Keep `SignInComponent.xaml` or `UpdatePasswordComponent.xaml` open as reference for patterns.

### Tip 4: Search Before Creating
Before thinking you need a new style, search existing styles in `Resources/Styles/*.xaml`

### Tip 5: Test As You Go
Don't wait until all files are done to test. Test each file individually.

---

## 🚨 Common Issues & Solutions

### Issue 1: "Resource Key Not Found"

**Error:** `Cannot find resource named 'Text.Something'`

**Solution:** Check spelling, ensure `Typography.xaml` is loaded in `App.xaml`

### Issue 2: "Styles Not Applying"

**Problem:** Element looks unstyled after change

**Solution:** 
1. Check resource key is correct
2. Rebuild project
3. Verify `App.xaml` merges all dictionaries

### Issue 3: "Theme Not Switching"

**Problem:** Some elements don't update with theme

**Solution:**
1. Check if using `StaticResource` (should be `DynamicResource` for colors)
2. Component styles use `DynamicResource` internally, so use styles
3. Verify `ThemeManager.ApplyTheme()` is called correctly

---

## 📊 Progress Tracking

Use this checklist to track your progress:

### Phase 1: Critical Auth (6 files)
- [x] SignInComponent.xaml - DONE
- [x] UpdatePasswordComponent.xaml - DONE
- [ ] CreateAccountComponent.xaml - **Next: Your review needed**
- [ ] ForgotPasswordComponent.xaml
- [ ] ResetPasswordComponent.xaml
- [ ] ForgotPasswordVerifyOtpComponent.xaml
- [ ] EmailVerificationComponent.xaml
- [ ] RecoverUsernameComponent.xaml

### Phase 2: UI Components (4 files)
- [ ] LaunchComponent.xaml
- [ ] TopNavigationBar.xaml
- [ ] BottomBar.xaml
- [ ] SupportComponent.xaml

### Phase 3: Features (~18 files)
- [ ] DetectionResultsScreen.xaml
- [ ] DetectionResultsComponent.xaml
- [ ] ... (others)

---

## 🎯 Your Next Step

**Start with CreateAccountComponent.xaml:**

1. Open `Controls/CreateAccountComponent.xaml`
2. Resources section is already empty ✅
3. Use find/replace table above to update content
4. Test the component
5. Test theme switching
6. Commit when successful

**Estimated Time:** 20-30 minutes

---

## 📞 Need Help?

If you encounter issues:
1. Check `DESIGN_SYSTEM_MIGRATION.md` for patterns
2. Look at `SignInComponent.xaml` for complete example
3. Review `THEME_IMPLEMENTATION_GUIDE.md` for theme details
4. Ask me to help with specific files or issues

---

**Let me know when CreateAccountComponent.xaml is complete, and I'll clean the next batch!** 🚀
