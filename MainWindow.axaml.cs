using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FontAppX;

public partial class MainWindow : Window
{
    List<GlyphInfo> Glyphs = new List<GlyphInfo>();

    readonly int FirstGlyph = 32; // SPACE (0x20) HARDCODED
    readonly int LastGlyph = 127; // DELETE (0x7f) HARDCODED
    readonly int GridRows = 6; // HARDCODED
    readonly int GridCols = 16; // HARDCODED

    SolidColorBrush GlyphIncludedBrush = new SolidColorBrush(Colors.CornflowerBlue);
    SolidColorBrush GlyphExdcludedBrush = new SolidColorBrush(Colors.DimGray);

    SKFontManager SystemFonts = SKFontManager.Default;

    List<string> FontList = new List<string>(); // List of all system fonts, populated by iterating `FontManager`

    string SystemFontName;
    string CustomFontName;

    bool UsingCustomFont;

    SKTypeface CustomFontTypeface;

    int TypefaceStyle; // Set from one of the following array members

    SKTypefaceStyle[] TypefaceStyles = { SKTypefaceStyle.Normal, SKTypefaceStyle.Bold, SKTypefaceStyle.Italic, SKTypefaceStyle.Bold | SKTypefaceStyle.Italic };

    int SmoothingMode;
    float OutlineWidth;
    float FontHeight;
    int FontSpacing;

    SKRect TextBounds = new SKRect();

    int FillColorR;
    int FillColorG;
    int FillColorB;
    SKColor FillColor;
    SKPaint FillPaint;

    int OutlineColorR;
    int OutlineColorG;
    int OutlineColorB;
    SKColor OutlineColor;
    SKPaint StrokePaint;

    int AtlasWidth;
    int AtlasHeight;

    int AtlasColorR;
    int AtlasColorG;
    int AtlasColorB;

    SKColor AtlasBackgroundColor;

    DrawableCanvas GlyphAtlasDisplay;
    DrawableCanvas GlyphAtlasFinal;

    Bitmap AboutLogoBitmap = new Bitmap("Assets/FontApp.png");

    bool CtrlHeld;
    bool ShiftHeld;
    bool AltHeld;

    bool UpdatingForm;

    string ProjectName;
    string ExportName;

    /// <summary>
    /// Initialize main window and install event handlers
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        Closing += MainWindow_Closing;
        Opened += MainWindow_Opened;

        KeyUp += MainWindow_KeyUp;
        KeyDown += MainWindow_KeyDown;

        SystemFontListBox.SelectionChanged += SystemFontListBox_SelectionChanged;
        ChooseCustomFontButton.Click += ChooseCustomFontButton_Click;
        CustomFontNameButton.Click += CustomFontNameButton_Click;

        IncludeAllGlyphsButton.Click += IncludeAllGlyphsButton_Click;
        ExcludeAllGlyphsButton.Click += ExcludeAllGlyphsButton_Click;

        FontStyleComboBox.SelectionChanged += FontStyleComboBox_SelectionChanged;
        SmoothingModeComboBox.SelectionChanged += SmoothingModeComboBox_SelectionChanged;
        FontHeightNumericUpDown.ValueChanged += FontHeightNumericUpDown_ValueChanged;
        FontSpacingNumericUpDown.ValueChanged += FontSpacingNumericUpDown_ValueChanged;
        OutlineWidthNumericUpDown.ValueChanged += OutlineWidthNumericUpDown_ValueChanged;

        FillColorRedSlider.GetPropertyChangedObservable(Slider.ValueProperty).AddClassHandler<Slider>((t, args) => FillColorRedSlider_PropertyChanged());
        FillColorGreenSlider.GetPropertyChangedObservable(Slider.ValueProperty).AddClassHandler<Slider>((t, args) => FillColorGreenSlider_PropertyChanged());
        FillColorBlueSlider.GetPropertyChangedObservable(Slider.ValueProperty).AddClassHandler<Slider>((t, args) => FillColorBlueSlider_PropertyChanged());
        FillColorButton.Click += FillColorButton_Click;
        FillColorButton.Flyout.Closed += ColorButtonFlyout_Closed;
        FillColorTextBox.KeyDown += ColorTextBox_KeyDown;
        FillColorTextBox.KeyUp += FillColorTextBox_KeyUp;

        OutlineColorRedSlider.GetPropertyChangedObservable(Slider.ValueProperty).AddClassHandler<Slider>((t, args) => OutlineColorRedSlider_PropertyChanged());
        OutlineColorGreenSlider.GetPropertyChangedObservable(Slider.ValueProperty).AddClassHandler<Slider>((t, args) => OutlineColorGreenSlider_PropertyChanged());
        OutlineColorBlueSlider.GetPropertyChangedObservable(Slider.ValueProperty).AddClassHandler<Slider>((t, args) => OutlineColorBlueSlider_PropertyChanged());
        OutlineColorButton.Click += OutlineColorButton_Click;
        OutlineColorButton.Flyout.Closed += ColorButtonFlyout_Closed;
        OutlineColorTextBox.KeyDown += ColorTextBox_KeyDown;
        OutlineColorTextBox.KeyUp += OutlineColorTextBox_KeyUp;

        AtlasColorRedSlider.GetPropertyChangedObservable(Slider.ValueProperty).AddClassHandler<Slider>((t, args) => AtlasColorRedSlider_PropertyChanged());
        AtlasColorGreenSlider.GetPropertyChangedObservable(Slider.ValueProperty).AddClassHandler<Slider>((t, args) => AtlasColorGreenSlider_PropertyChanged());
        AtlasColorBlueSlider.GetPropertyChangedObservable(Slider.ValueProperty).AddClassHandler<Slider>((t, args) => AtlasColorBlueSlider_PropertyChanged());
        AtlasColorButton.Click += AtlasColorButton_Click;
        AtlasColorButton.Flyout.Closed += ColorButtonFlyout_Closed;
        AtlasColorTextBox.KeyDown += ColorTextBox_KeyDown;
        AtlasColorTextBox.KeyUp += AtlasColorTextBox_KeyUp;

        NewProjectMenuItem.Click += NewProjectMenuItem_Click;
        OpenProjectMenuItem.Click += OpenProjectMenuItem_Click;
        SaveProjectMenuItem.Click += SaveProjectMenuItem_Click;
        SaveProjectAsMenuItem.Click += SaveProjectAsMenuItem_Click;
        ExitFontAppMenuItem.Click += ExitFontAppMenuItem_Click;
        ExportProjectMenuItem.Click += ExportProjectMenuItem_Click;
        ExportProjectAsMenuItem.Click += ExportProjectAsMenuItem_Click;
        AboutFontAppMenuItem.Click += AboutFontAppMenuItem_Click;
    }
    /// <summary>
    /// Initialize application
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void MainWindow_Opened(object? sender, EventArgs e)
    {
        FillPaint = new SKPaint();
        FillPaint.Style = SKPaintStyle.Fill;

        StrokePaint = new SKPaint();
        StrokePaint.Style = SKPaintStyle.Stroke;
        StrokePaint.StrokeCap = SKStrokeCap.Round;

        for (int j = FirstGlyph; j <= LastGlyph; j++) Glyphs.Add(new GlyphInfo(j)); // Create list of glyph infos

        // Populate `GlyphSelectionGrid` with buttons that facilitate including and excluding glyphs
        var i = 0;
        for (int r = 0; r < GridRows; r++)
        {
            for (int c = 0; c < GridCols; c++)
            {
                Button button = new Button();
                button.HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center;
                button.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
                button.Margin = new Thickness(0, 0, 0, 0);
                button.Background = GlyphIncludedBrush;
                button.Content = (i != LastGlyph - FirstGlyph) ? Glyphs[i].AsciiChar : " ";

                Glyphs[i].GridButton = button; // The glyph should know who its associated button is
                button.Tag = Glyphs[i++]; // And the button should know who it's associated glyph is

                // Install button `Click` event handler
                button.Click += (s, e) =>
                {
                    GlyphInfo glyphInfo = button.Tag as GlyphInfo;

                    glyphInfo.Include = !glyphInfo.Include; // Toggle included / excluded
                    button.Background = (glyphInfo.Include) ? GlyphIncludedBrush : GlyphExdcludedBrush; // Set background color accordingly

                    DoTheMagic();
                };

                // Add to grid at desired coordinates
                Grid.SetRow(button, r); // Position
                Grid.SetColumn(button, c);
                GlyphSelectionGrid.Children.Add(button); // Add
            }
        }
        NewProject(); // Reset everything
    }
    /// <summary>
    /// Cleanup
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        AboutLogoBitmap?.Dispose();
        FillPaint?.Dispose();
        StrokePaint?.Dispose();
        SystemFonts.Dispose();
        CustomFontTypeface?.Dispose();
    }
    private void NewProject()
    {
        UpdatingForm = true;

        ProjectName = null;

        UsingCustomFont = false;
        CustomFontNameButton.Content = "";

        SystemFontName = null;
        CustomFontName = null;

        SystemFontListBox.SelectedIndex = -1; // Repopulate system font list
        SystemFontListBox.Items = null;
        FontList.Clear();
        foreach (var font in SystemFonts.GetFontFamilies()) FontList.Add(font);
        FontList.Sort();
        SystemFontListBox.Items = FontList;

        FillColorR = 255;
        FillColorG = 255;
        FillColorB = 255;
        FillColorRedSlider.Value = FillColorR;
        FillColorGreenSlider.Value = FillColorG;
        FillColorBlueSlider.Value = FillColorB;
        UpdateFillColor();

        OutlineColorR = 0;
        OutlineColorG = 0;
        OutlineColorB = 0;
        FillColorRedSlider.Value = FillColorR;
        FillColorGreenSlider.Value = FillColorG;
        FillColorBlueSlider.Value = FillColorB;
        UpdateOutlineColor();

        AtlasColorR = 127;
        AtlasColorG = 127;
        AtlasColorB = 127;
        AtlasColorRedSlider.Value = AtlasColorR;
        AtlasColorGreenSlider.Value = AtlasColorG;
        AtlasColorBlueSlider.Value = AtlasColorB;
        UpdateAtlasColor();

        TypefaceStyle = 0;
        FontStyleComboBox.SelectedIndex = TypefaceStyle;

        SmoothingMode = 1;
        SmoothingModeComboBox.SelectedIndex = SmoothingMode;

        OutlineWidth = 0;
        OutlineWidthNumericUpDown.Value = OutlineWidth;

        FontHeight = 32;
        FontHeightNumericUpDown.Value = FontHeight;

        FontSpacing = 2;
        FontSpacingNumericUpDown.Value = FontSpacing;

        // Export Options
        ExportFormatComboBox.SelectedIndex = 0;
        IncludeFontNameCheckBox.IsChecked = true;
        IncludeGlyphRangeCheckBox.IsChecked = true;
        IncludeGlyphSpacingCheckBox.IsChecked = true;

        foreach (var glyph in Glyphs) glyph.Include = true;

        IncludeOrExcludeAllGlyphs(true);

        GlyphAtlasCanvasContainer.Content = null;

        UpdatingForm = false;

        DoTheMagic();
    }
    private void Log(string message) => Debug.WriteLine(message);

    #region Font Options
    private void FontHeightNumericUpDown_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (UpdatingForm) return;
        FontHeight = (int)FontHeightNumericUpDown.Value;
        DoTheMagic();
    }
    private void FontSpacingNumericUpDown_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (UpdatingForm) return;
        FontSpacing = (int)FontSpacingNumericUpDown.Value;
    }
    private void FontStyleComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (UpdatingForm || FontStyleComboBox.SelectedIndex == -1) return;
        TypefaceStyle = FontStyleComboBox.SelectedIndex;
        DoTheMagic();
    }
    private void SmoothingModeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (UpdatingForm || SmoothingModeComboBox.SelectedIndex == -1) return;
        SmoothingMode = SmoothingModeComboBox.SelectedIndex;
        DoTheMagic();
    }
    private void OutlineWidthNumericUpDown_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (UpdatingForm) return;
        OutlineWidth = (int)OutlineWidthNumericUpDown.Value;
        DoTheMagic();
    }
    private void IncludeAllGlyphsButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => IncludeOrExcludeAllGlyphs(true);
    private void ExcludeAllGlyphsButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => IncludeOrExcludeAllGlyphs(false);
    /// <summary>
    /// Include or exclude all glyphs according to the given state
    /// </summary>
    /// <param name="includeOrExclude"></param>
    private void IncludeOrExcludeAllGlyphs(bool includeOrExclude)
    {
        foreach (var glyphInfo in Glyphs)
        {
            glyphInfo.Include = includeOrExclude; // Toggle included / excluded
            glyphInfo.GridButton.Background = (glyphInfo.Include) ? GlyphIncludedBrush : GlyphExdcludedBrush; // Toggle button background color to match included flag
        }
        DoTheMagic();
    }
    #endregion

    #region Fill and outline color sliders
    private void UpdateFillColor()
    {
        var fillColorForeground = GetColorString(FillColorR, FillColorG, FillColorB);
        var fillColorBackground = GetInvertedColorString(FillColorR, FillColorG, FillColorB);
        FillColor = SKColor.Parse(fillColorForeground);
        FillColorButton.Foreground = new SolidColorBrush(Color.Parse($"#{fillColorBackground}"));
        FillColorButton.Background = new SolidColorBrush(Color.Parse($"#{fillColorForeground}"));
        FillColorButton.Content = fillColorForeground;
        FillColorTextBox.Text = fillColorBackground;
    }
    private void UpdateOutlineColor()
    {
        var OutlineColorForeground = GetColorString(OutlineColorR, OutlineColorG, OutlineColorB);
        var OutlineColorBackground = GetInvertedColorString(OutlineColorR, OutlineColorG, OutlineColorB);
        OutlineColor = SKColor.Parse(OutlineColorForeground);
        OutlineColorButton.Foreground = new SolidColorBrush(Color.Parse($"#{OutlineColorBackground}"));
        OutlineColorButton.Background = new SolidColorBrush(Color.Parse($"#{OutlineColorForeground}"));
        OutlineColorButton.Content = OutlineColorForeground;
        OutlineColorTextBox.Text = OutlineColorBackground;
    }
    private void UpdateAtlasColor()
    {
        var AtlasColorForeground = GetColorString(AtlasColorR, AtlasColorG, AtlasColorB);
        var AtlasColorBackground = GetInvertedColorString(AtlasColorR, AtlasColorG, AtlasColorB);
        var atlasColor = new SolidColorBrush(Color.Parse($"#{AtlasColorForeground}"));
        AtlasBackgroundColor = SKColor.Parse($"#{AtlasColorForeground}");
        AtlasColorButton.Foreground = new SolidColorBrush(Color.Parse($"#{AtlasColorBackground}"));
        AtlasColorButton.Background = atlasColor;
        AtlasColorButton.Content = AtlasColorForeground;
        GlyphAtlasCanvasContainer.Background = atlasColor;
        AtlasColorTextBox.Text = AtlasColorBackground;
    }
    private void FillColorRedSlider_PropertyChanged()
    {
        if (UpdatingForm) return;

        var value = (int)FillColorRedSlider.Value;
        if (value != FillColorR)
        {
            FillColorR = value;
            UpdateFillColor();
        }
    }
    private void FillColorGreenSlider_PropertyChanged()
    {
        if (UpdatingForm) return;

        var value = (int)FillColorGreenSlider.Value;
        if (value != FillColorG)
        {
            FillColorG = value;
            UpdateFillColor();
        }
    }
    private void FillColorBlueSlider_PropertyChanged()
    {
        if (UpdatingForm) return;

        var value = (int)FillColorBlueSlider.Value;
        if (value != FillColorB)
        {
            FillColorB = value;
            UpdateFillColor();
        }
    }
    private void OutlineColorRedSlider_PropertyChanged()
    {
        if (UpdatingForm) return;

        var value = (int)OutlineColorRedSlider.Value;
        if (value != OutlineColorR)
        {
            OutlineColorR = value;
            UpdateOutlineColor();
        }
    }
    private void OutlineColorGreenSlider_PropertyChanged()
    {
        if (UpdatingForm) return;

        var value = (int)OutlineColorGreenSlider.Value;
        if (value != OutlineColorG)
        {
            OutlineColorG = value;
            UpdateOutlineColor();
        }
    }
    private void OutlineColorBlueSlider_PropertyChanged()
    {
        if (UpdatingForm) return;

        var value = (int)OutlineColorBlueSlider.Value;
        if (value != OutlineColorB)
        {
            OutlineColorB = value;
            UpdateOutlineColor();
        }
    }
    private void AtlasColorRedSlider_PropertyChanged()
    {
        if (UpdatingForm) return;

        var value = (int)AtlasColorRedSlider.Value;
        if (value != AtlasColorR)
        {
            AtlasColorR = value;
            UpdateAtlasColor();
            //DoTheMagic();
        }
    }
    private void AtlasColorGreenSlider_PropertyChanged()
    {
        if (UpdatingForm) return;

        var value = (int)AtlasColorGreenSlider.Value;
        if (value != AtlasColorG)
        {
            AtlasColorG = value;
            UpdateAtlasColor();
            //DoTheMagic();
        }
    }
    private void AtlasColorBlueSlider_PropertyChanged()
    {
        if (UpdatingForm) return;

        var value = (int)AtlasColorBlueSlider.Value;
        if (value != AtlasColorB)
        {
            AtlasColorB = value;
            UpdateAtlasColor();
            //DoTheMagic();
        }
    }
    private void FillColorButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => FillColorTextBox.Text = $"{FillColorR.ToString("X2")}{FillColorG.ToString("X2")}{FillColorB.ToString("X2")}";
    private void OutlineColorButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => OutlineColorTextBox.Text = $"{OutlineColorR.ToString("X2")}{OutlineColorG.ToString("X2")}{OutlineColorB.ToString("X2")}";
    private void AtlasColorButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => AtlasColorTextBox.Text = $"{AtlasColorR.ToString("X2")}{AtlasColorG.ToString("X2")}{AtlasColorB.ToString("X2")}";
    private void FillColorTextBox_KeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            FillColorButton.Flyout.Hide();
            return;
        }
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            FillColorButton.Flyout.Hide();
            var text = FillColorTextBox.Text;
            if (text.Length == 6)
            {
                FillColorR = Convert.ToInt32(text.Substring(0, 2), 16);
                FillColorG = Convert.ToInt32(text.Substring(2, 2), 16);
                FillColorB = Convert.ToInt32(text.Substring(4, 2), 16);
            }
            UpdateFillColor();
            DoTheMagic();
        }
    }
    private void OutlineColorTextBox_KeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            OutlineColorButton.Flyout.Hide();
            return;
        }
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            OutlineColorButton.Flyout.Hide();
            var text = OutlineColorTextBox.Text;
            if (text.Length == 6)
            {
                OutlineColorR = Convert.ToInt32(text.Substring(0, 2), 16);
                OutlineColorG = Convert.ToInt32(text.Substring(2, 2), 16);
                OutlineColorB = Convert.ToInt32(text.Substring(4, 2), 16);
            }
            UpdateOutlineColor();
            DoTheMagic();
        }
    }
    private void AtlasColorTextBox_KeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            AtlasColorButton.Flyout.Hide();
            return;
        }
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            AtlasColorButton.Flyout.Hide();
            var text = AtlasColorTextBox.Text;
            if (text.Length == 6)
            {
                AtlasColorR = Convert.ToInt32(text.Substring(0, 2), 16);
                AtlasColorG = Convert.ToInt32(text.Substring(2, 2), 16);
                AtlasColorB = Convert.ToInt32(text.Substring(4, 2), 16);
            }
            UpdateAtlasColor();
            DoTheMagic();
        }
    }
    private void ColorTextBox_KeyDown(object? sender, KeyEventArgs e) // NOTE: Handles all color buttons (FillColorButton, OutlineColorButton, and AtlasColorButton)
    {
        if (Regex.IsMatch(e.Key.ToString(), "[^0-9a-fA-F]")) e.Handled = true;
    }
    private void ColorButtonFlyout_Closed(object? sender, EventArgs e) => DoTheMagic(); // NOTE: Handles all color buttons (FillColorButton, OutlineColorButton, and AtlasColorButton)
    private string GetInvertedColorString(int r, int g, int b) => $"{0x80 ^ r:X2}{0x80 ^ g:X2}{0x80 ^ b:X2}";
    private string GetColorString(int r, int g, int b) => $"{r & 255:X2}{g & 255:X2}{b & 255:X2}";
    /// <summary>
    /// Regenerate atlas if custom font is set
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void CustomFontNameButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!UsingCustomFont)
        {
            SystemFontListBox.SelectedIndex = -1;
            SystemFontName = null;
            UsingCustomFont = true;

            SetCutsomFontNameSelected(true);
            SetFont();
        }
    }
    /// <summary>
    /// Initiate custom font selection
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ChooseCustomFontButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ChooseCustomFont();
    /// <summary>
    /// Allow user to select custom font
    /// </summary>
    /// <returns></returns>
    private async Task ChooseCustomFont()
    {
        var dialog = new OpenFileDialog();

        dialog.Filters.Add(new FileDialogFilter() { Name = "Font Files", Extensions = { "ttf", "otf" } });

        dialog.Title = "Choose Custom Font File";

        var fileNames = await dialog.ShowAsync(this);

        if (fileNames != null)
        {
            var path = fileNames[0];

            CustomFontTypeface?.Dispose();
            CustomFontTypeface = SKTypeface.FromFile(path);
            //Log($"font-family:{CustomFontTypeface.FamilyName}");

            CustomFontNameButton.Content = CustomFontTypeface.FamilyName;

            CustomFontName = path;

            UsingCustomFont = true;

            // NOTE: There is probably a better way to accomplish the following task

            TypefaceStyle = 0;
            if (CustomFontTypeface.IsBold) TypefaceStyle = 1;
            if (CustomFontTypeface.IsItalic) TypefaceStyle = 2;
            if (CustomFontTypeface.IsBold && CustomFontTypeface.IsItalic) TypefaceStyle = 3;

            SetCutsomFontNameSelected(true);

            SetFont();
        }
    }
    private void SetCutsomFontNameSelected(bool selected)
    {
        if (selected)
        {
            CustomFontNameButton.Background = Brushes.CornflowerBlue;
            CustomFontNameButton.Foreground = Brushes.WhiteSmoke;
        }
        else
        {
            CustomFontNameButton.Background = Brushes.WhiteSmoke;
            CustomFontNameButton.Foreground = Brushes.Black;
        }
    }
    /// <summary>
    /// User selcted a system font
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void SystemFontListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SystemFontListBox.SelectedIndex == -1) return;

        SystemFontName = SystemFontListBox.SelectedItem.ToString();
        UsingCustomFont = false;

        SetCutsomFontNameSelected(false);

        SetFont();
    }
    private void SetFont()
    {
        // NOTE: Does there actually need to be anything else in here?
        DoTheMagic();
    }
    #endregion

    #region Keyboard events (menu hotkeys)
    private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl) CtrlHeld = true;
        else if (e.Key == Key.LeftAlt || e.Key == Key.RightAlt) AltHeld = true;
        else if (e.Key == Key.LeftShift || e.Key == Key.RightShift) ShiftHeld = true;
    }
    private void MainWindow_KeyUp(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.N:
                if (CtrlHeld) NewProject();
                break;

            case Key.O:
                if (CtrlHeld) OpenProject();
                break;

            case Key.S:
                if (CtrlHeld)
                {
                    if (ShiftHeld)
                    {
                        SaveProjectAs();
                    }
                    else
                    {
                        SaveProject();
                    }
                }
                break;

            case Key.E:
                if (CtrlHeld)
                {
                    if (ShiftHeld)
                    {
                        ExportProjectAs();
                    }
                    else
                    {
                        ExportProject();
                    }
                }
                break;

            case Key.F1:
                ShowAboutWindow();
                break;

            case Key.F4:
                Close();
                break;

            case Key.LeftCtrl:
            case Key.RightCtrl:
                CtrlHeld = false;
                break;

            case Key.LeftAlt:
            case Key.RightAlt:
                AltHeld = false;
                break;

            case Key.LeftShift:
            case Key.RightShift:
                ShiftHeld = false;
                break;

            default:
                break;
        }
    }
    #endregion

    #region Menus
    private void NewProjectMenuItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => NewProject();
    private void OpenProjectMenuItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => OpenProject();
    private void SaveProjectMenuItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SaveProject();
    private void SaveProjectAsMenuItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SaveProjectAs();
    private void ExportProjectMenuItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ExportProject();
    private void ExportProjectAsMenuItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ExportProjectAs();
    private void ExitFontAppMenuItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
    private void AboutFontAppMenuItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ShowAboutWindow();
    #endregion

    private void ShowAboutWindow()
    {
        var window = new AboutWindow();
        window.Position = new PixelPoint(Position.X + 16, Position.Y + 16);
        window.AboutLogo.Source = AboutLogoBitmap;
        window.HomePageButton.Click += (s, e) => { OpenBrowser("https://github.com/Antix-Development/FontAppX"); };
        window.SponsorPageButton.Click += (s, e) => { OpenBrowser("https://www.buymeacoffee.com/antixdevelu"); };
        window.OkayButton.Click += (s, e) => { window.Close(); };
        window.KeyUp += (s, e) => { if (e.Key == Key.Enter || e.Key == Key.Return || e.Key == Key.Escape) window.Close(); };
        window.ShowDialog(this);
    }
    /// <summary>
    /// Convert the given string to a bool
    /// </summary>
    /// <param name="s"></param>
    /// <returns></returns>
    bool StringToBool(string s) => s == "1";
    /// <summary>
    /// Convert the given string to a bool
    /// </summary>
    /// <param name="b"></param>
    /// <returns></returns>
    string BoolToString(bool b) => b ? "1" : "0";
    /// <summary>
    /// Prompt for filename and open project
    /// </summary>
    private async Task OpenProject()
    {
        var dialog = new OpenFileDialog();
        dialog.Filters.Add(new FileDialogFilter() { Name = "FontApp Files", Extensions = { "fap" } });
        dialog.Title = "Choose Project";
        var fileNames = await dialog.ShowAsync(this);
        if (fileNames != null)
        {
            UpdatingForm = true;

            ProjectName = fileNames[0];

            int includedGlyphCount = 0;

            foreach (var line in File.ReadLines(ProjectName))
            {
                //Log(line);

                string[] values;

                string[] parts = line.Split(new string[] { "=" }, StringSplitOptions.None);

                switch (parts[0])
                {
                    case "SYSTEM_FONTNAME":
                        SystemFontName = parts[1];
                        break;

                    case "USE_CUSTOM_FONT":
                        UsingCustomFont = StringToBool(parts[1]);
                        SetCutsomFontNameSelected(UsingCustomFont);
                        break;

                    case "CUSTOM_FONTNAME":
                        CustomFontName = parts[1];
                        CustomFontNameButton.Content = (Path.GetFileNameWithoutExtension(parts[1]));
                        break;

                    case "RENDERING_MODE":
                        SmoothingMode = Convert.ToInt32(parts[1]);
                        SmoothingModeComboBox.SelectedIndex = SmoothingMode;
                        break;

                    case "SPACING":
                        FontSpacingNumericUpDown.Value = Convert.ToInt32(parts[1]);
                        break;

                    case "FONT_STYLE":
                        FontStyleComboBox.SelectedIndex = Convert.ToInt32(parts[1]);
                        break;

                    case "FONT_SIZE":
                        FontHeight = Convert.ToInt32(parts[1]);
                        FontHeightNumericUpDown.Value = FontHeight;
                        break;

                    case "OUTLINE_WIDTH":
                        OutlineWidth = Convert.ToInt32(parts[1]);
                        OutlineWidthNumericUpDown.Value = OutlineWidth;
                        break;

                    case "OUTLINE_COLOR":
                        values = parts[1].Split(new string[] { "," }, StringSplitOptions.None);
                        Log($"parts: {values[0]},{values[1]},{values[2]}");
                        OutlineColorR = Convert.ToInt32(values[0]);
                        OutlineColorG = Convert.ToInt32(values[1]);
                        OutlineColorB = Convert.ToInt32(values[2]);
                        UpdateOutlineColor();
                        break;

                    case "FILL_COLOR":
                        values = parts[1].Split(new string[] { "," }, StringSplitOptions.None);
                        FillColorR = Convert.ToInt32(values[0]);
                        FillColorG = Convert.ToInt32(values[1]);
                        FillColorB = Convert.ToInt32(values[2]);
                        UpdateFillColor();
                        break;

                    case "ATLAS_BACKGROUND_COLOR":
                        values = parts[1].Split(new string[] { "," }, StringSplitOptions.None);
                        AtlasColorR = Convert.ToInt32(values[0]);
                        AtlasColorG = Convert.ToInt32(values[1]);
                        AtlasColorB = Convert.ToInt32(values[2]);
                        UpdateAtlasColor();
                        break;

                    case "EXPORT_FORMAT":
                        ExportFormatComboBox.SelectedIndex = Convert.ToInt32(parts[1]);
                        break;

                    case "INCLUDE_FONTNAME":
                        IncludeFontNameCheckBox.IsChecked = StringToBool(parts[1]);
                        break;

                    case "INCLUDE_GLYPH_SPACING":
                        IncludeGlyphSpacingCheckBox.IsChecked = StringToBool(parts[1]);
                        break;

                    case "INCLUDE_GLYPH_INDICES":
                        IncludeGlyphRangeCheckBox.IsChecked = StringToBool(parts[1]);
                        break;

                    case "GLYPH":
                        values = parts[1].Split(new string[] { "," }, StringSplitOptions.None);
                        var glyphInfo = Glyphs[Convert.ToInt32(values[0]) - FirstGlyph];
                        glyphInfo.Include = StringToBool(values[1]);
                        includedGlyphCount += (glyphInfo.Include) ? 1 : 0;
                        break;

                    default:
                        //Log($"OpenProject() Unknown property {parts[0]}");
                        break;
                }
            }

            if (UsingCustomFont)
            {
                SetCutsomFontNameSelected(true);
            }
            else
            {
                SetCutsomFontNameSelected(false);

                for (int i = 0; i < FontList.Count; i++)
                {
                    if (FontList[i] == SystemFontName)
                    {
                        SystemFontListBox.SelectedIndex = i;
                        break;
                    }
                }
            }

            UpdatingForm = false;
            DoTheMagic();
        }
    }
    /// <summary>
    /// Save the project, prompting for filename
    /// </summary>
    private async Task SaveProjectAs()
    {
        if (CustomFontName == null && SystemFontName == null) return;

        var dialog = new SaveFileDialog();

        dialog.Filters.Add(new FileDialogFilter() { Name = "FontApp Files", Extensions = { "fap" } });

        dialog.Title = "Save Project As";

        var fileName = await dialog.ShowAsync(this);

        if (fileName != null)
        {
            ProjectName = fileName;
            SaveProject();
        }
    }
    /// <summary>
    /// Save the project using the current filename
    /// </summary>
    private void SaveProject()
    {
        if (ProjectName != null)
        {
            try
            {
                ProjectName = (ProjectName.EndsWith(".fap")) ? ProjectName.Substring(0, ProjectName.Length - 4) : ProjectName;

                StreamWriter writer = new StreamWriter($"{ProjectName}.fap");

                writer.WriteLine($"SYSTEM_FONTNAME={SystemFontName}");

                writer.WriteLine($"CUSTOM_FONTNAME={CustomFontName}");

                writer.WriteLine($"USE_CUSTOM_FONT={BoolToString(UsingCustomFont)}");

                writer.WriteLine($"FIRSTGLYPH={FirstGlyph}");

                writer.WriteLine($"LASTGLYPH={LastGlyph}");

                writer.WriteLine($"RENDERING_MODE={SmoothingMode}");

                writer.WriteLine($"SPACING={FontSpacingNumericUpDown.Value}");

                writer.WriteLine($"FONT_STYLE={FontStyleComboBox.SelectedIndex}");

                writer.WriteLine($"FONT_SIZE={FontHeightNumericUpDown.Value}");

                writer.WriteLine($"OUTLINE_WIDTH={OutlineWidthNumericUpDown.Value}");

                writer.WriteLine($"OUTLINE_COLOR={OutlineColorR},{OutlineColorG},{OutlineColorB}");

                writer.WriteLine($"ATLAS_BACKGROUND_COLOR={AtlasColorR},{AtlasColorG},{AtlasColorB}");

                writer.WriteLine($"FILL_COLOR={FillColorR},{FillColorG},{FillColorB}");

                writer.WriteLine($"EXPORT_FORMAT={ExportFormatComboBox.SelectedIndex}");

                writer.WriteLine($"INCLUDE_FONTNAME={BoolToString((bool)IncludeFontNameCheckBox.IsChecked)}");

                writer.WriteLine($"INCLUDE_GLYPH_SPACING={BoolToString((bool)IncludeGlyphSpacingCheckBox.IsChecked)}");

                writer.WriteLine($"INCLUDE_GLYPH_INDICES={BoolToString((bool)IncludeGlyphRangeCheckBox.IsChecked)}");

                foreach (GlyphInfo glyphInfo in Glyphs)
                {
                    writer.WriteLine($"GLYPH={glyphInfo.CharCode},{BoolToString(glyphInfo.Include)}");
                }

                writer.Close();
                writer.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SaveProject(){Environment.NewLine}{ex.ToString()}");
                throw;
            }
        }
        else
        {
            SaveProjectAs();
        }
    }
    private void ExportProject()
    {
        if (ExportName != null)
        {
            try
            {
                if (ExportName.EndsWith(".txt"))
                {
                    ExportName = ExportName.Substring(0, ExportName.Length - 4);
                }
                else if (ExportName.EndsWith(".json"))
                {
                    ExportName = ExportName.Substring(0, ExportName.Length - 5);
                }

                var renderBitmap = new RenderTargetBitmap(new PixelSize((int)GlyphAtlasFinal.Bounds.Width, (int)GlyphAtlasFinal.Bounds.Height));
                renderBitmap.Render(GlyphAtlasFinal);
                renderBitmap.Save($"{ExportName}.png");
                renderBitmap.Dispose();

                //Output_PictureBox.Image.Save($"{ExportName}.png", ImageFormat.Png);

                string fontName = (UsingCustomFont) ? Path.GetFileNameWithoutExtension(CustomFontName) : SystemFontName;

                if (ExportFormatComboBox.SelectedIndex == 0) // Text
                {
                    StreamWriter writer = new StreamWriter($"{ExportName}.txt");

                    if ((bool)IncludeFontNameCheckBox.IsChecked) writer.WriteLine($"FONTNAME={fontName}");
                    if ((bool)IncludeGlyphSpacingCheckBox.IsChecked) writer.WriteLine($"SPACING={FontSpacing}");
                    if ((bool)IncludeGlyphRangeCheckBox.IsChecked)
                    {
                        writer.WriteLine($"FIRSTGLYPH={FirstGlyph}");
                        writer.WriteLine($"LASTGLYPH={LastGlyph}");
                    }

                    foreach (GlyphInfo glyphInfo in Glyphs)
                    {
                        if (glyphInfo.Include && glyphInfo.CharCode != 127)
                        {
                            writer.WriteLine($"GLYPH={glyphInfo.CharCode},{glyphInfo.X + 1},{glyphInfo.Y + 1},{glyphInfo.Width - 2},{glyphInfo.Height - 2}");
                        }
                    }
                    writer.Close();
                }
                else // JSON
                {
                    StreamWriter writer = new StreamWriter($"{ExportName}.json");

                    writer.WriteLine("{");
                    if ((bool)IncludeFontNameCheckBox.IsChecked) writer.WriteLine($"  \"FontName\": \"{fontName}\",");
                    if ((bool)IncludeGlyphSpacingCheckBox.IsChecked) writer.WriteLine($"  \"GlyphSpacing\": {FontSpacing},");
                    if ((bool)IncludeGlyphRangeCheckBox.IsChecked)
                    {
                        writer.WriteLine($"  \"FirstGlyph\": {FirstGlyph},");
                        writer.WriteLine($"  \"LastGlyph\": {LastGlyph},");
                    }
                    writer.WriteLine($"  \"Glyphs\":[");

                    // Create temp list because we need to bugger about with the last glyph exported
                    List<GlyphInfo> tempGlyphs = new List<GlyphInfo>();
                    foreach (GlyphInfo glyphInfo in Glyphs)
                    {
                        if (glyphInfo.Include && glyphInfo.CharCode != 127) tempGlyphs.Add(glyphInfo);
                    }

                    for (int i = 0; i < tempGlyphs.Count - 1; i++)
                    {
                        var tempGlyph = tempGlyphs[i];
                        writer.WriteLine("    {");
                        writer.WriteLine($"      \"CharCode\": {tempGlyph.CharCode},");
                        writer.WriteLine($"      \"X\": {tempGlyph.X + 1},");
                        writer.WriteLine($"      \"Y\": {tempGlyph.Y + 1},");
                        writer.WriteLine($"      \"W\": {tempGlyph.Width - 2},");
                        writer.WriteLine($"      \"H\": {tempGlyph.Height - 2}");
                        writer.WriteLine("    },");
                    }

                    var lastGlyph = tempGlyphs[tempGlyphs.Count - 1];
                    writer.WriteLine("    {");
                    writer.WriteLine($"      \"CharCode\": {lastGlyph.CharCode},");
                    writer.WriteLine($"      \"X\": {lastGlyph.X + 1},");
                    writer.WriteLine($"      \"Y\": {lastGlyph.Y + 1},");
                    writer.WriteLine($"      \"W\": {lastGlyph.Width - 2},");
                    writer.WriteLine($"      \"H\": {lastGlyph.Height - 2}");
                    writer.WriteLine("    }"); // << The buggery bit where we need to omit the final comma

                    writer.WriteLine("  ]");
                    writer.WriteLine("}");

                    writer.Close();
                }
            }
            catch (Exception ex)
            {
                Log($"Export(){Environment.NewLine}{ex}");
                throw;
            }
        }
        else
        {
            ExportProjectAs();
        }
    }
    private async Task ExportProjectAs()
    {
        if (CustomFontName == null && SystemFontName == null) return;

        var dialog = new SaveFileDialog();

        dialog.Filters.Add(new FileDialogFilter() { Name = "Export Files", Extensions = { "*.*" } });

        dialog.Title = "Export Project As";

        var fileName = await dialog.ShowAsync(this);

        if (fileName != null)
        {
            ExportName = fileName;

            ExportProject();
        }
    }
    public static void OpenBrowser(string url)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo { FileName = $"{url}", UseShellExecute = true });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Process.Start("xdg-open", url);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", url);
        }
        else
        {
        }
    }
    /// <summary>
    /// Regenerate the atlas
    /// </summary>
    private void DoTheMagic()
    {
        if (CustomFontName == null && SystemFontName == null) return;

        var typeFace = (UsingCustomFont) ? SKTypeface.FromFile(CustomFontName) : SKTypeface.FromFamilyName(SystemFontName, TypefaceStyles[TypefaceStyle]);

        FillPaint.Typeface = typeFace;
        FillPaint.TextSize = FontHeight;
        FillPaint.IsAntialias = (SmoothingMode == 1) ? true : false;
        FillPaint.Color = FillColor;

        StrokePaint.Typeface = typeFace;
        StrokePaint.TextSize = FontHeight;
        StrokePaint.IsAntialias = (SmoothingMode == 1) ? true : false;
        StrokePaint.Color = OutlineColor;
        StrokePaint.StrokeWidth = OutlineWidth;

        UpdateTextBounds("!#$%&'()*+,-./0125456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[]^_`abcdefghijklmnopqrstuvwxyz{|}~");

        var glyphHeight = (int)TextBounds.Height; // Height of tallest glyph
        var glyphBaseline = (int)Math.Abs(TextBounds.Bottom);

        //Log($"AllChars:{TextBounds} Left:{TextBounds.Left} Top:{TextBounds.Top} Right:{TextBounds.Right} Bottom:{TextBounds.Bottom} Location:{TextBounds.Location}");
        
        for (int i = 0; i < LastGlyph - FirstGlyph + 1; i++)
        {
            var glyph = Glyphs[i];

            if (glyph.Include)
            {
                UpdateTextBounds(glyph.AsciiChar);

                //Log($"{glyph.AsciiChar} {TextBounds.Top + TextBounds.Height} Left:{TextBounds.Left} Top:{TextBounds.Top} Width:{TextBounds.Width} Height:{TextBounds.Height} Bottom:{TextBounds.Bottom} MidY:{TextBounds.MidY}");

                glyph.X = 0;
                glyph.Y = 0;
                glyph.Width = (i == 0) ? 8 : (int)TextBounds.Width; // +2 padding
                glyph.Height = glyphHeight; // +2 padding
                glyph.DestX = (int)TextBounds.Left;
                glyph.DestY = glyphHeight - glyphBaseline;
            }
        }

        PackRects();

        Glyphs.Sort((a, b) => (a.CharCode.CompareTo(b.CharCode))); // Sort back into ascending order

        //foreach (var glyph in Glyphs) Log($"{glyph.CharCode}, {glyph.AsciiChar}, {glyph.X}, {glyph.Y} {glyph.Width}, {glyph.Height}");

        /*
        Unfortunately at this point we need to get super "kludgey" use two `DrawableCanvas` controls because...

        If we call `canvas.Clear(SKColors.Transparent)`, the area of the canvas will be cleared and we will be abble to see into the desktop!
        This is because the canvases parent `GlyphAtlasContainer` has already rendered its background, so when the canvas calls `Clear()' the area is nuked.

        The solution requires using one `DrawableCanvas` for displaying which has its background cleared using the same background color as `GlyphAtlasContainer`, 
        and another `DrawableCanvas` used for exporting which has a transparent background. The second `DrawableCanvas` is not visible.

        Essentially we are doubling our drawing which is so totally sub optimal, but I don't think there is any way to resolve this without rewriting the entire 
        thing using something other than `DrawableCanvas` and rendering the text with some other mechanism.
        */

        // Create the atlas that will be displayed
        GlyphAtlasDisplay = new DrawableCanvas(AtlasWidth, AtlasHeight);
        GlyphAtlasDisplay.RenderSkia += (SKCanvas canvas) =>
        {
            canvas.Clear(AtlasBackgroundColor);

            for (int i = 0; i < LastGlyph - FirstGlyph; i++)
            {
                var glyph = Glyphs[i];
                if (glyph.Include)
                {
                    var renderX = glyph.X - glyph.DestX + 1;
                    var renderY = glyph.Y + glyph.DestY + 1;
                    canvas.DrawText(glyph.AsciiChar, renderX, renderY, FillPaint);
                    if (OutlineWidth > 0) canvas.DrawText(glyph.AsciiChar, renderX, renderY, StrokePaint);
                }
            }
        };

        // Create the atlas that will be exported
        GlyphAtlasFinal = new DrawableCanvas(AtlasWidth, AtlasHeight);
        GlyphAtlasFinal.RenderSkia += (SKCanvas canvas) =>
        {
            canvas.Clear(SKColors.Transparent);
            for (int i = 0; i < LastGlyph - FirstGlyph; i++)
            {
                var glyph = Glyphs[i];
                if (glyph.Include)
                {
                    var renderX = glyph.X - glyph.DestX + 1;
                    var renderY = glyph.Y + glyph.DestY + 1;
                    canvas.DrawText(glyph.AsciiChar, renderX, renderY, FillPaint);
                    if (OutlineWidth > 0) canvas.DrawText(glyph.AsciiChar, renderX, renderY, StrokePaint);
                }
            }
        };

        GlyphAtlasCanvasContainer.Content = GlyphAtlasDisplay; // Display atlas canvas

        GlyphAtlasFinal.InvalidateVisual();
        GlyphAtlasDisplay.InvalidateVisual();
    }
    private void UpdateTextBounds(string s)
    {
        if (OutlineWidth > 0) // Results will differ whether outlined or not
        {
            StrokePaint.MeasureText(s, ref TextBounds);
        }
        else
        {
            FillPaint.MeasureText(s, ref TextBounds);
        }
    }
    /// <summary>
    /// PackRects() is a C# conversion of potpack, a tiny JavaScript library for packing 
    /// 2D rectangles into a near-square container (https://github.com/mapbox/potpack)
    /// 
    /// ISC License
    /// 
    /// Copyright(c) 2022, Mapbox
    /// 
    /// Permission to use, copy, modify, and/or distribute this software for any purpose
    /// with or without fee is hereby granted, provided that the above copyright notice
    /// and this permission notice appear in all copies.
    /// 
    /// THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH
    /// REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY AND
    /// FITNESS.IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT,
    /// INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS
    /// OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER
    /// TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE OF
    /// THIS SOFTWARE.
    /// 
    /// </summary>
    public void PackRects()
    {
        int area = 0;
        int maxWidth = 0;

        foreach (var glyphInfo in Glyphs)
        {
            if (glyphInfo.Include && glyphInfo.CharCode != 127)
            {
                glyphInfo.X = 0;
                glyphInfo.Y = 0;
                glyphInfo.Width += 2; // Add padding
                glyphInfo.Height += 2;

                area += glyphInfo.Width * glyphInfo.Height; // Add to area

                maxWidth = Math.Max(maxWidth, glyphInfo.Width); // Find widest
            }
        }

        Glyphs.Sort((a, b) => (b.Height.CompareTo(a.Height))); // Sort highest > shortest

        float startWidth = (float)Math.Max(Math.Ceiling(Math.Sqrt(area / 0.95)), maxWidth); // Start with a single empty space, unbounded at the bottom

        int height = 0;
        int width = 0;

        List<GlyphInfo> spaces = new List<GlyphInfo>();

        spaces.Add(new GlyphInfo(0, 0, 0, (int)startWidth, int.MaxValue));

        foreach (var box in Glyphs)
        {
            if (box.Include && box.CharCode != 127)
            {
                // look through spaces backwards so that we check smaller spaces first
                for (var i = spaces.Count - 1; i >= 0; i--)
                {
                    var space = spaces[i];

                    // look for empty spaces that can accommodate the current box
                    if (box.Width > space.Width || box.Height > space.Height) continue;

                    // found the space; add the box to its top-left corner
                    // |-------|-------|
                    // |  box  |       |
                    // |_______|       |
                    // |         space |
                    // |_______________|
                    box.X = space.X;
                    box.Y = space.Y;

                    height = Math.Max(height, box.Y + box.Height);
                    width = Math.Max(width, box.X + box.Width);

                    if (box.Width == space.Width && box.Height == space.Height)
                    {
                        // space matches the box exactly; remove it
                        var last = spaces[spaces.Count - 1];
                        spaces.RemoveAt(spaces.Count - 1);

                        if (i < spaces.Count) spaces[i] = last;

                    }
                    else if (box.Height == space.Height)
                    {
                        // space matches the box height; update it accordingly
                        // |-------|---------------|
                        // |  box  | updated space |
                        // |_______|_______________|
                        space.X += box.Width;
                        space.Width -= box.Width;

                    }
                    else if (box.Width == space.Width)
                    {
                        // space matches the box width; update it accordingly
                        // |---------------|
                        // |      box      |
                        // |_______________|
                        // | updated space |
                        // |_______________|
                        space.Y += box.Height;
                        space.Height -= box.Height;

                    }
                    else
                    {
                        // otherwise the box splits the space into two spaces
                        // |-------|-----------|
                        // |  box  | new space |
                        // |_______|___________|
                        // | updated space     |
                        // |___________________|
                        spaces.Add(new GlyphInfo(0, space.X + box.Width, space.Y, space.Width - box.Width, box.Height));

                        space.Y += box.Height;
                        space.Height -= box.Height;
                    }
                    break;
                }
            }
        }

        AtlasWidth = width;
        AtlasHeight = height;
    }
}