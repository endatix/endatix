using Azure.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;

namespace Endatix.Persistence.SqlServer.Security;

public class SqlEncryptionInitializer
{
    private const string AZURE_TENANT_ID = "";
    private const string AZURE_CLIENT_ID = "";
    private const string AZURE_CLIENT_SECRET = "";
    private static bool _initialized;
    private static readonly object _lock = new();

    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        lock (_lock)
        {
            if (_initialized)
            {
                return;
            }

            var clientCredential = new ClientSecretCredential(AZURE_TENANT_ID, AZURE_CLIENT_ID, AZURE_CLIENT_SECRET);
            var akvProvider = new SqlColumnEncryptionAzureKeyVaultProvider(clientCredential);

            Dictionary<string, SqlColumnEncryptionKeyStoreProvider> providers =
                new(capacity: 1)
                {
                    { SqlColumnEncryptionAzureKeyVaultProvider.ProviderName, akvProvider }
                };

            SqlConnection.RegisterColumnEncryptionKeyStoreProviders(providers);
            _initialized = true;
        }
    }
}
