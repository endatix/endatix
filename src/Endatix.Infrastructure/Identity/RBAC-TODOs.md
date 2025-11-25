# RBAC TODOs

This is created as part of the [Role Based Access Control (RBAC) #261](https://github.com/endatix/endatix-private/issues/261) to track TODOs pending completion as we get ready to merge the RBAC changes into the `main` branch.

## TODOs

- [x] Remove UnifiedTestEndpoint
- [x] Add Keycloak AuthorizationProvider
- [x] Rename JwtClaimsTransformer to ClaimsTransformer
- [x] Add Endatix ClaimsIdentity for auth logic isolation and improved security
- [x] Clean up AuthorizationData from Cache metadata
- [x] Add DI and registraiton infrastructure for AuthorizationProviders
- [ ] Extend ProviderRegistration with Interface and proper auth providers validation and runtime instance providing logic
- [x] Add tests
- [x] Cover AuthorizationData with tests
- [x] Cover ClaimsTransformer with tests
- [x] Cover AssertionPermissionsHandler with tests
- [x] Cover PlatformAdminHandler and TenantAdminHandler with tests
- [x] Cover CurrentUserAuthorizationService with tests
- [x] Cover AuthorizationCache with tests
- [x] Cover DefaultAuthorization with tests
- [x] Cover AuthorizedIdentity with tests
- [x] Cover ClaimsPrincipalExtensions with tests
- [x] Update InfrastructureSecurityBuilder Tests to include new Auth services
- [x] Cover KeycloakTokenIntrospectionAuthorization with tests


## Pretty Level TODOs

- [ ] Split Actions.cs into Access.cs, Platform.cs, and Actions.cs partial classes if possible to improve readability

## Questions/Suggestions

- Permission Category Questions should be generalized to Plugin or Survey Extensions e.g. expressions, calculations, etc.
- Shall we store the tenantId in the cache key or userId is sufficient?
- Review if the Cache Invalidation cannot be separated from the PermissionService?
