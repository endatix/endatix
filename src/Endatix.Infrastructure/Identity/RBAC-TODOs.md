# RBAC TODOs

This is created as part of the [Role Based Access Control (RBAC) #261](https://github.com/endatix/endatix-private/issues/261) to track TODOs pending completion as we get ready to merge the RBAC changes into the `main` branch.

## TODOs

- [x] Fix migration issue with AppRoleId column
- [x] Add Platform vs Tenant Admin roles basic isolation logic
- [x] Add fast access.authenticated permission basic authentication level authorization
- [x] Add access.apps.hub to the backend permissions
- [x] Add auth/me endpoint to return permissions
- [x] Remove UserInfo endpoint
- [ ] Add seeding of Admin, Creator roles
- [ ] Remove UnifiedTestEndpoint
- [ ] Add cache invalidation on user login and roles/user related changes (where relevant)
- [ ] Clean up `IPermissionService` public interface and API once we are closer to production. At this point we will really know what we will need and what not to expose to the client.
- [ ] Refactor PermissionService, AssertionPermissionsHandler, & JwtClaimsTransformer to optimize DB query logic
- [ ] Use context.User to get UserId instead of using IUserContext


## Pretty Level TODOs
- [ ] Split Actions.cs into Access.cs, Platform.cs, and Actions.cs partial classes if possible to improve readability

## Questions/Suggestions
- Permission Category Questions should be generalized to Plugin or Survey Extensions e.g. expressions, calculations, etc.
- My Account - This should be "Authenticated" level, not permission-based
- No special permission needed for self-management e.g. change password, change email, etc. - AllowAnonymous() for these endpoints
- Shall we store the tenantId in the cache key or userId is sufficient?
- Review if the Cache Invalidation cannot be separated from the PermissionService?
- Discuss if we need hierarchical roles and permissions at this stage?