using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BarcaLog.Api.Infra;

/// <summary>Aceita "HH:mm" (formato do &lt;input type="time"&gt; do frontend) além de "HH:mm:ss"; escreve "HH:mm".</summary>
public class ConversorTimeOnly : JsonConverter<TimeOnly>
{
    private static readonly string[] Formatos = ["HH:mm", "HH:mm:ss", "H:mm"];

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var texto = reader.GetString();
        if (TimeOnly.TryParseExact(texto, Formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out var hora)) return hora;
        throw new JsonException($"Horário inválido: '{texto}'. Use HH:mm.");
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(value.Second == 0 ? "HH:mm" : "HH:mm:ss", CultureInfo.InvariantCulture));
}
