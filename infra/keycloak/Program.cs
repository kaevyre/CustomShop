using Keycloak.Iac;
using Keycloak.Iac.Factories;
using Pulumi;
using Pulumi.Keycloak.OpenId;
using KcProvider = Pulumi.Keycloak.Provider;
using KcProviderArgs = Pulumi.Keycloak.ProviderArgs;
using KcRealm = Pulumi.Keycloak.Realm;
using KcRealmArgs = Pulumi.Keycloak.RealmArgs;
using KcRole = Pulumi.Keycloak.Role;
using KcRoleArgs = Pulumi.Keycloak.RoleArgs;
using KcUser = Pulumi.Keycloak.User;
using KcUserArgs = Pulumi.Keycloak.UserArgs;

const string RealmName = "DevRealmDrivers";

return await Deployment.RunAsync(() =>
{
    var config = new Config();
    var keycloakUrl = config.Get("keycloakUrl") ?? "http://localhost:8080";
    var adminUser = config.Get("adminUser") ?? "admin";
    var adminPassword = config.RequireSecret("adminPassword");
    var testUserPassword = config.GetSecret("testUserPassword") ?? Output.CreateSecret("ChangeMe123!");

    // ───────── Provider: підключення до Keycloak (контейнер з AppHost, http://localhost:8080)
    var provider = new KcProvider("keycloak", new KcProviderArgs
    {
        ClientId = "admin-cli",
        Username = adminUser,
        Password = adminPassword,
        Url = keycloakUrl
    });
    var opts = new CustomResourceOptions { Provider = provider };

    // ───────── 3.2 Realm DevRealmDrivers
    var realm = new KcRealm("realm", new KcRealmArgs
    {
        RealmName = RealmName,
        Enabled = true,
        DisplayName = "Dev Realm Drivers",
        RegistrationAllowed = true,
        ResetPasswordAllowed = true,
        RememberMe = true,
        LoginWithEmailAllowed = true,
        VerifyEmail = false,
        LoginTheme = "keycloak",
        AccountTheme = "keycloak.v3",
        AdminTheme = "keycloak.v2",
        EmailTheme = "keycloak"
    }, opts);

    // Дозволяємо власний user attribute "authorization" (JSON з permissions) — з нього мапер будує claim токена
    var userProfile = new Pulumi.Keycloak.RealmUserProfile("user-profile", new Pulumi.Keycloak.RealmUserProfileArgs
    {
        RealmId = realm.Id,
        UnmanagedAttributePolicy = "ENABLED",
        Attributes =
        {
            new Pulumi.Keycloak.Inputs.RealmUserProfileAttributeArgs
            {
                Name = "username",
                DisplayName = "${username}",
                Permissions = new Pulumi.Keycloak.Inputs.RealmUserProfileAttributePermissionsArgs
                    { Views = { "admin", "user" }, Edits = { "admin", "user" } }
            },
            new Pulumi.Keycloak.Inputs.RealmUserProfileAttributeArgs
            {
                Name = "email",
                DisplayName = "${email}",
                Permissions = new Pulumi.Keycloak.Inputs.RealmUserProfileAttributePermissionsArgs
                    { Views = { "admin", "user" }, Edits = { "admin", "user" } }
            },
            new Pulumi.Keycloak.Inputs.RealmUserProfileAttributeArgs
            {
                Name = "firstName",
                DisplayName = "${firstName}",
                Permissions = new Pulumi.Keycloak.Inputs.RealmUserProfileAttributePermissionsArgs
                    { Views = { "admin", "user" }, Edits = { "admin", "user" } }
            },
            new Pulumi.Keycloak.Inputs.RealmUserProfileAttributeArgs
            {
                Name = "lastName",
                DisplayName = "${lastName}",
                Permissions = new Pulumi.Keycloak.Inputs.RealmUserProfileAttributePermissionsArgs
                    { Views = { "admin", "user" }, Edits = { "admin", "user" } }
            }
        }
    }, opts);
    var afterProfile = new CustomResourceOptions { Provider = provider, DependsOn = { userProfile } };

    // ───────── Realm-ролі: admin, manager, user, guest
    var roles = PermissionMatrix.Roles.ToDictionary(
        name => name,
        name => new KcRole($"role-{name}", new KcRoleArgs
        {
            RealmId = realm.Id,
            Name = name,
            Description = $"Realm role '{name}'"
        }, opts));

    // ───────── 3.3 Resource Servers: scopes → resources → role policies → permissions (Permission Policies)
    // client-level ролі кожного Resource Server (reader/writer) і composite "full" — для service accounts (п. 3.3)
    var clientReaders = new Dictionary<string, (Client Server, KcRole Reader)>();

    foreach (var (resourceServer, scopeNames) in PermissionMatrix.ResourceServers)
    {
        var server = new Client($"rs-{resourceServer}", new ClientArgs
        {
            RealmId = realm.Id,
            ClientId = resourceServer,
            Name = resourceServer,
            Enabled = true,
            AccessType = "CONFIDENTIAL",
            StandardFlowEnabled = false,
            ServiceAccountsEnabled = true, // обов'язково для Authorization Services
            Authorization = new Pulumi.Keycloak.OpenId.Inputs.ClientAuthorizationArgs
            {
                PolicyEnforcementMode = "ENFORCING",       // Permission Evaluation mode
                DecisionStrategy = "UNANIMOUS",
                AllowRemoteResourceManagement = true,
                KeepDefaults = false
            }
        }, opts);

        var reader = new KcRole($"{resourceServer}-client-role-reader", new KcRoleArgs
        {
            RealmId = realm.Id, ClientId = server.Id, Name = "reader",
            Description = $"Client role: read access to {resourceServer}"
        }, opts);
        var writer = new KcRole($"{resourceServer}-client-role-writer", new KcRoleArgs
        {
            RealmId = realm.Id, ClientId = server.Id, Name = "writer",
            Description = $"Client role: write access to {resourceServer}"
        }, opts);
        // composite role: групує reader + writer в один «повний» набір
        _ = new KcRole($"{resourceServer}-client-role-full", new KcRoleArgs
        {
            RealmId = realm.Id, ClientId = server.Id, Name = "full",
            Description = $"Composite: reader + writer on {resourceServer}",
            CompositeRoles = { reader.Id, writer.Id }
        }, opts);
        clientReaders[resourceServer] = (server, reader);

        var scopes = scopeNames.ToDictionary(
            scope => scope,
            scope => new ClientAuthorizationScope($"{resourceServer}-scope-{scope.Replace(':', '-')}",
                new ClientAuthorizationScopeArgs
                {
                    RealmId = realm.Id,
                    ResourceServerId = server.Id,
                    Name = scope
                }, opts));

        var resource = new ClientAuthorizationResource($"{resourceServer}-resource",
            new ClientAuthorizationResourceArgs
            {
                RealmId = realm.Id,
                ResourceServerId = server.Id,
                Name = resourceServer,
                DisplayName = $"{resourceServer} resource",
                Scopes = scopeNames
            },
            new CustomResourceOptions { Provider = provider, DependsOn = scopes.Values.Cast<Resource>().ToList() });

        // Permission Policy на кожну роль, що має доступ до цього Resource Server (таблиця 7)
        foreach (var role in PermissionMatrix.Roles)
        {
            var granted = PermissionMatrix.Granted(role, resourceServer);
            if (granted.Length == 0) continue; // напр. payments-api: manager/user/guest — без permissions

            var policy = new ClientRolePolicy($"{resourceServer}-policy-{role}", new ClientRolePolicyArgs
            {
                RealmId = realm.Id,
                ResourceServerId = server.Id,
                Name = $"{role}-role-policy",
                DecisionStrategy = "UNANIMOUS",
                Logic = "POSITIVE",
                Type = "role",
                Roles = new[]
                {
                    new Pulumi.Keycloak.OpenId.Inputs.ClientRolePolicyRoleArgs { Id = roles[role].Id, Required = false }
                }
            }, opts);

            var permissionArgs = new ClientAuthorizationPermissionArgs
            {
                RealmId = realm.Id,
                ResourceServerId = server.Id,
                Name = $"{role}-{resourceServer}-permission",
                DecisionStrategy = "UNANIMOUS",
                Policies = { policy.Id },
                Resources = { resource.Id }
            };
            foreach (var scope in granted) permissionArgs.Scopes.Add(scopes[scope].Id);

            _ = new ClientAuthorizationPermission($"{resourceServer}-permission-{role}", permissionArgs, opts);
        }
    }

    // ───────── Client scopes: orders_mgmt / products_mgmt / payments_mgmt + "permissions" (claim authorization)
    var mgmtScopes = new[] { "orders_mgmt", "products_mgmt", "payments_mgmt" }
        .Select(name => new ClientScope($"scope-{name}", new ClientScopeArgs
        {
            RealmId = realm.Id,
            Name = name,
            Description = $"Management scope {name}",
            IncludeInTokenScope = true
        }, opts))
        .ToList();

    var permissionsScope = new ClientScope("scope-permissions", new ClientScopeArgs
    {
        RealmId = realm.Id,
        Name = "permissions",
        Description = "Adds claim authorization.permissions to the access token",
        IncludeInTokenScope = false
    }, opts);

    // Protocol Mapper: user attribute "authorization" (JSON) → claim "authorization" (JSON object) в access token
    _ = new UserAttributeProtocolMapper("mapper-permissions", new UserAttributeProtocolMapperArgs
    {
        RealmId = realm.Id,
        ClientScopeId = permissionsScope.Id,
        Name = "authorization-permissions",
        UserAttribute = "authorization",
        ClaimName = "authorization",
        ClaimValueType = "JSON",
        AddToAccessToken = true,
        AddToIdToken = false,
        AddToUserinfo = false
    }, opts);

    // ───────── 3.4 Clients (factory pattern)
    var defaultScopeNames = mgmtScopes.Select(s => s.Name).Append(permissionsScope.Name).ToList();
    var factory = new ClientFactory(realm.Id, opts, defaultScopeNames);

    var postman = factory.CreateAuthorizationCodeClient("orders.postman", "Postman (Authorization Code)",
        ["https://oauth.pstmn.io/v1/callback"], ["+"], isPublic: false);
    var swagger = factory.CreateAuthorizationCodeClient("orders.swagger", "Swagger UI (public, PKCE)",
        ["http://localhost:5080/swagger/oauth2-redirect.html", "https://localhost:5001/swagger/oauth2-redirect.html"],
        ["http://localhost:5080", "https://localhost:5001"], isPublic: true);
    var bff = factory.CreateAuthorizationCodeClient("orders.bff", "Orders BFF (Gateway)",
        ["https://localhost:7000/signin-oidc"], ["https://localhost:7000"], isPublic: false);
    var aggregator = factory.CreateClientCredentialsClient("aggregator.service", "Aggregator (Client Credentials)");

    // service account Aggregator: client-level ролі "reader" на orders-api і products-api (M2M — лише читання)
    foreach (var rs in new[] { "orders-api", "products-api" })
    {
        var (server, reader) = clientReaders[rs];
        _ = new ClientServiceAccountRole($"aggregator-{rs}-reader", new ClientServiceAccountRoleArgs
        {
            RealmId = realm.Id,
            ServiceAccountUserId = aggregator.ServiceAccountUserId,
            ClientId = server.Id,
            Role = reader.Name
        }, opts);
    }

    // ───────── Тестові користувачі: по одному на роль; тимчасовий пароль, підтверджений email, UserRoles
    foreach (var testUser in TestUsers.All)
    {
        var user = new KcUser($"user-{testUser.Username}", new KcUserArgs
        {
            RealmId = realm.Id,
            Username = testUser.Username,
            Enabled = true,
            Email = testUser.Email,
            EmailVerified = true,
            FirstName = testUser.FirstName,
            LastName = testUser.LastName,
            InitialPassword = new Pulumi.Keycloak.Inputs.UserInitialPasswordArgs
            {
                Value = testUserPassword,
                Temporary = false          // dev: пароль одразу робочий (без примусової зміни при першому вході)
            },
            // Permissions користувача за матрицею його ролі → claim "authorization" у токені
            Attributes =
            {
                ["authorization"] = PermissionMatrix.BuildAuthorizationJson([testUser.Role])
            }
        }, afterProfile);

        _ = new Pulumi.Keycloak.UserRoles($"userroles-{testUser.Username}", new Pulumi.Keycloak.UserRolesArgs
        {
            RealmId = realm.Id,
            UserId = user.Id,
            RoleIds = { roles[testUser.Role].Id },
            Exhaustive = false
        }, opts);
    }

    // ───────── 3.6 Outputs
    return new Dictionary<string, object?>
    {
        ["RealmUrl"] = $"{keycloakUrl}/realms/{RealmName}",
        ["AdminConsoleUrl"] = $"{keycloakUrl}/admin/master/console/#/{RealmName}",
        ["ClientIds"] = new[] { "orders.postman", "orders.swagger", "orders.bff", "aggregator.service" },
        ["ClientSecrets"] = new Dictionary<string, object?>
        {
            ["orders.postman"] = postman.ClientSecret,
            ["orders.bff"] = bff.ClientSecret,
            ["aggregator.service"] = aggregator.ClientSecret
        }
    };
});
