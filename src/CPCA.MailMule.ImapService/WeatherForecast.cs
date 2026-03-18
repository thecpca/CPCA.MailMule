namespace CPCA.MailMule.ApiService;

public record WeatherForecast(DateOnly Date, Int32 TemperatureC, String? Summary)
{
    public Int32 TemperatureF => 32 + (Int32)(TemperatureC / 0.5556);
}
