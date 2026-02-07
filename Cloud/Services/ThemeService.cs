namespace Cloud.Services;

/// <summary>
/// Service per gestire i temi CSS dell'applicazione
/// Configurabile tramite appsettings.json
/// </summary>
public class ThemeService
{
    private readonly string? _themeName;
    private readonly ILogger<ThemeService> _logger;
    
    public ThemeService(IConfiguration configuration, ILogger<ThemeService> logger)
    {
        _themeName = configuration.GetValue<string?>("Theme");
    _logger = logger;
        
        if (!string.IsNullOrWhiteSpace(_themeName))
        {
 _logger.LogInformation($"Theme Service initialized with theme: {_themeName}");
        }
        else
{
  _logger.LogInformation("Theme Service initialized - No theme active (using default Bootstrap)");
    }
    }
 
    /// <summary>
    /// Restituisce il nome del tema attivo (null = theme disabilitato)
 /// </summary>
    public string? ActiveTheme => _themeName;
    
    /// <summary>
    /// Restituisce il path del file CSS del tema attivo
    /// </summary>
    public string? ThemeCssPath => string.IsNullOrWhiteSpace(_themeName) 
        ? null 
        : $"themes/{_themeName.ToLower()}.css";

    /// <summary>
    /// Verifica se un tema è attivo
    /// </summary>
    public bool IsThemeActive => !string.IsNullOrWhiteSpace(_themeName);
    
    /// <summary>
    /// Lista dei temi disponibili
    /// </summary>
  public static readonly string[] AvailableThemes = new[]
    {
     "Modern",
        "Dark", 
        "Compact",
        "Professional"
    };
    
    /// <summary>
    /// Ottiene informazioni sul tema attivo
  /// </summary>
    public string GetThemeInfo()
    {
      if (!IsThemeActive)
     return "Default Bootstrap Theme";
    
        return _themeName switch
        {
        "Modern" => "Modern Theme - Gradient design with smooth animations",
            "Dark" => "Dark Theme - Perfect for low-light environments",
     "Compact" => "Compact Theme - Dense layout for maximum information density",
     "Professional" => "Professional Theme - Clean corporate design",
     _ => $"Custom Theme: {_themeName}"
    };
    }
}
