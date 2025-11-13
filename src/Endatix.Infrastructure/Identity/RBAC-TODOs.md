# RBAC TODOs

This is created as part of the [Role Based Access Control (RBAC) #261](https://github.com/endatix/endatix-private/issues/261) to track TODOs pending completion as we get ready to merge the RBAC changes into the `main` branch.

## TODOs

- [ ] Remove UnifiedTestEndpoint
- [x] Add Keycloak AuthorizationProvider
- [x] Rename JwtClaimsTransformer to ClaimsTransformer
- [ ] Add Endatix ClaimsIdentity for auth logic isolation and improved security
- [ ] Clean up AuthorizationData from Cache metadata
- [ ] Add DI and registraiton infrastructure for AuthorizationProviders
- [ ] Extend ProviderRegistration with Interface and proper auth providers validation and runtime instance providing logic
- [ ] Add tests

## Pretty Level TODOs

- [ ] Split Actions.cs into Access.cs, Platform.cs, and Actions.cs partial classes if possible to improve readability

## Questions/Suggestions

- Permission Category Questions should be generalized to Plugin or Survey Extensions e.g. expressions, calculations, etc.
- Shall we store the tenantId in the cache key or userId is sufficient?
- Review if the Cache Invalidation cannot be separated from the PermissionService?
