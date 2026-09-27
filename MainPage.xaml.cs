using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Maui.Networking;

namespace MeuAppTempo;

public partial class MainPage : ContentPage
{
    private static readonly HttpClient cliente = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    public MainPage()
    {
        InitializeComponent();
    }

    private async void BuscarPrevisao_Clicked(object sender, EventArgs e)
    {
        string cidade = txtCidade.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(cidade))
        {
            lblResultado.Text = "Digite o nome de uma cidade.";
            return;
        }

        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            lblResultado.Text = "Sem conexão com a internet.";
            return;
        }

        carregando.IsVisible = true;
        carregando.IsRunning = true;
        lblResultado.Text = "Buscando previsão...";

        try
        {
            string cidadeCodificada = Uri.EscapeDataString(cidade);
            string urlCidade =
                $"https://geocoding-api.open-meteo.com/v1/search?name={cidadeCodificada}&count=1&language=pt&format=json";

            using HttpResponseMessage respostaCidade =
                await cliente.GetAsync(urlCidade);

            if (respostaCidade.StatusCode == HttpStatusCode.NotFound)
            {
                lblResultado.Text = "Cidade não encontrada.";
                return;
            }

            if (!respostaCidade.IsSuccessStatusCode)
            {
                lblResultado.Text =
                    $"O serviço de cidades respondeu com erro: {(int)respostaCidade.StatusCode}.";
                return;
            }

            string jsonCidade =
                await respostaCidade.Content.ReadAsStringAsync();

            using JsonDocument dadosCidade = JsonDocument.Parse(jsonCidade);

            if (!dadosCidade.RootElement.TryGetProperty(
                    "results", out JsonElement resultados) ||
                resultados.GetArrayLength() == 0)
            {
                lblResultado.Text = "Não encontrei essa cidade. Confira o nome.";
                return;
            }

            JsonElement primeiraCidade = resultados[0];
            double latitude = primeiraCidade.GetProperty("latitude").GetDouble();
            double longitude = primeiraCidade.GetProperty("longitude").GetDouble();

            string nomeCidade = primeiraCidade.GetProperty("name").GetString() ?? cidade;
            string estado = primeiraCidade.TryGetProperty(
                "admin1", out JsonElement estadoJson)
                ? estadoJson.GetString() ?? ""
                : "";

            string latitudeTexto =
                latitude.ToString(CultureInfo.InvariantCulture);
            string longitudeTexto =
                longitude.ToString(CultureInfo.InvariantCulture);

            string urlClima =
                "https://api.open-meteo.com/v1/forecast" +
                $"?latitude={latitudeTexto}&longitude={longitudeTexto}" +
                "&current=temperature_2m,relative_humidity_2m,apparent_temperature,weather_code,wind_speed_10m" +
                "&timezone=auto";

            using HttpResponseMessage respostaClima =
                await cliente.GetAsync(urlClima);

            if (!respostaClima.IsSuccessStatusCode)
            {
                lblResultado.Text =
                    $"O serviço do clima respondeu com erro: {(int)respostaClima.StatusCode}.";
                return;
            }

            string jsonClima =
                await respostaClima.Content.ReadAsStringAsync();

            using JsonDocument dadosClima = JsonDocument.Parse(jsonClima);
            JsonElement climaAtual =
                dadosClima.RootElement.GetProperty("current");

            double temperatura =
                climaAtual.GetProperty("temperature_2m").GetDouble();
            double sensacao =
                climaAtual.GetProperty("apparent_temperature").GetDouble();
            int umidade =
                climaAtual.GetProperty("relative_humidity_2m").GetInt32();
            double vento =
                climaAtual.GetProperty("wind_speed_10m").GetDouble();
            int codigoClima =
                climaAtual.GetProperty("weather_code").GetInt32();

            string descricao = DescreverClima(codigoClima);
            string local = string.IsNullOrWhiteSpace(estado)
                ? nomeCidade
                : $"{nomeCidade} - {estado}";

            lblResultado.Text =
                $"Previsão para {local}\n" +
                $"{descricao}\n" +
                $"Temperatura: {temperatura.ToString("F1", CultureInfo.GetCultureInfo("pt-BR"))} °C\n" +
                $"Sensação: {sensacao.ToString("F1", CultureInfo.GetCultureInfo("pt-BR"))} °C\n" +
                $"Umidade: {umidade}%\n" +
                $"Vento: {vento.ToString("F1", CultureInfo.GetCultureInfo("pt-BR"))} km/h";
        }
        catch (HttpRequestException)
        {
            lblResultado.Text =
                "Não consegui conectar ao serviço de previsão. Tente novamente mais tarde.";
        }
        catch (TaskCanceledException)
        {
            lblResultado.Text =
                "A consulta demorou demais e foi cancelada. Tente novamente.";
        }
        catch (JsonException)
        {
            lblResultado.Text =
                "O serviço retornou dados que o aplicativo não conseguiu ler.";
        }
        catch (Exception)
        {
            lblResultado.Text =
                "Ocorreu um erro ao buscar a previsão.";
        }
        finally
        {
            carregando.IsRunning = false;
            carregando.IsVisible = false;
        }
    }

    private static string DescreverClima(int codigo)
    {
        return codigo switch
        {
            0 => "Céu limpo",
            1 or 2 or 3 => "Parcialmente nublado",
            45 or 48 => "Neblina",
            51 or 53 or 55 => "Garoa",
            61 or 63 or 65 => "Chuva",
            71 or 73 or 75 => "Neve",
            80 or 81 or 82 => "Pancadas de chuva",
            95 or 96 or 99 => "Trovoadas",
            _ => "Condição do tempo não identificada"
        };
    }
}