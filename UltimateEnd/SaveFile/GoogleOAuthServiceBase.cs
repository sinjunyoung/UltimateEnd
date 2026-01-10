using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using UltimateEnd.Models;
using UltimateEnd.Services;

namespace UltimateEnd.SaveFile
{
    public abstract class GoogleOAuthServiceBase : IGoogleOAuthService
    {
        protected const string Scope = "https://www.googleapis.com/auth/drive.file";
        protected const string TokenFilePath = "google_tokens.json";

        protected readonly HttpClient _httpClient;
        protected string _codeVerifier;

        public string AccessToken { get; protected set; }

        public string RefreshToken { get; protected set; }

        public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);

        protected abstract string ClientId { get; }

        protected abstract string ClientSecret { get; }

        protected abstract string RedirectUri { get; }

        protected GoogleOAuthServiceBase()
        {
            _httpClient = new HttpClient();
        }

        public async Task<bool> TryAuthenticateFromStoredTokenAsync()
        {
            string root = AppBaseFolderProviderFactory.Create?.Invoke().GetAppBaseFolder();
            var tokenPath = Path.Combine(root, TokenFilePath);

            if (!File.Exists(tokenPath))
                return false;

            try
            {
                var json = await File.ReadAllTextAsync(tokenPath);
                var tokens = JsonSerializer.Deserialize<StoredTokens>(json);

                if (tokens != null && !string.IsNullOrEmpty(tokens.RefreshToken))
                {
                    RefreshToken = tokens.RefreshToken;

                    return await RefreshAccessTokenAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load stored tokens: {ex.Message}");
            }

            return false;
        }

        protected async Task<bool> RefreshAccessTokenAsync()
        {
            if (string.IsNullOrEmpty(RefreshToken))
                return false;

            KeyValuePair<string, string> [] parameters =
            [
                new ("client_id", ClientId),
                new ("refresh_token", RefreshToken),
                new ("grant_type", "refresh_token")
            ];

            List<KeyValuePair<string, string>> parameterList = [.. parameters];

            if (!string.IsNullOrEmpty(ClientSecret))
                parameterList.Add(new KeyValuePair<string, string>("client_secret", ClientSecret));

            FormUrlEncodedContent content = new(parameterList);

            var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", content);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var token = JsonSerializer.Deserialize<GoogleTokenResponse>(json);

                if (token != null && !string.IsNullOrEmpty(token.AccessToken))
                {
                    AccessToken = token.AccessToken;

                    return true;
                }
            }

            return false;
        }

        protected static string GenerateCodeVerifier()
        {
            var randomBytes = new byte[32];
            RandomNumberGenerator.Fill(randomBytes);

            return Base64UrlEncode(randomBytes);
        }

        protected static string GenerateCodeChallenge(string codeVerifier)
        {
            var bytes = Encoding.ASCII.GetBytes(codeVerifier);
            var hash = SHA256.HashData(bytes);

            return Base64UrlEncode(hash);
        }

        protected static string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        protected string BuildAuthorizationUrl()
        {
            _codeVerifier = GenerateCodeVerifier();
            var codeChallenge = GenerateCodeChallenge(_codeVerifier);

            return $"https://accounts.google.com/o/oauth2/v2/auth?" +
                   $"client_id={ClientId}&" +
                   $"redirect_uri={Uri.EscapeDataString(RedirectUri)}&" +
                   $"response_type=code&" +
                   $"scope={Uri.EscapeDataString(Scope)}&" +
                   $"code_challenge={codeChallenge}&" +
                   $"code_challenge_method=S256&" +
                   $"access_type=offline&" +
                   $"prompt=consent";
        }

        protected async Task<bool> ExchangeCodeForTokenAsync(string code)
        {
            if (string.IsNullOrEmpty(code))
                return false;

            KeyValuePair<string, string> [] parameters =
            [
                new ("code", code),
                new ("client_id", ClientId),
                new ("redirect_uri", RedirectUri),
                new ("code_verifier", _codeVerifier),
                new ("grant_type", "authorization_code")
            ];

            List<KeyValuePair<string, string>> parameterList = [.. parameters];

            if (!string.IsNullOrEmpty(ClientSecret))
                parameterList.Add(new KeyValuePair<string, string>("client_secret", ClientSecret));

            FormUrlEncodedContent content = new(parameterList);

            var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", content);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var token = JsonSerializer.Deserialize<GoogleTokenResponse>(json);

                if (token != null)
                {
                    AccessToken = token.AccessToken;
                    RefreshToken = token.RefreshToken;

                    await SaveTokensAsync();

                    return true;
                }
            }

            return false;
        }

        protected async Task SaveTokensAsync()
        {
            string root = AppBaseFolderProviderFactory.Create?.Invoke().GetAppBaseFolder();
            var tokenPath = Path.Combine(root, TokenFilePath);
            var tokens = new StoredTokens { RefreshToken = RefreshToken };
            var json = JsonSerializer.Serialize(tokens);

            await File.WriteAllTextAsync(tokenPath, json);
        }

        public abstract Task<bool> AuthenticateAsync();
    }
}