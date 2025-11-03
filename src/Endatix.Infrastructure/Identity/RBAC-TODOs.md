# RBAC TODOs

This is created as part of the [Role Based Access Control (RBAC) #261](https://github.com/endatix/endatix-private/issues/261) to track TODOs pending completion as we get ready to merge the RBAC changes into the `main` branch.

## TODOs

- [x] Fix migration issue with AppRoleId column
- [ ] Add seeding of Admin, Creator roles
- [ ] Add Platform vs Tenant Admin roles basic isolation logic
- [ ] Add fast access.authenticated permission basic authentication level authorization
- [ ] Add cache invalidation on user login and roles/user related changes (where relevant)
- [ ] Remove UnifiedTestEndpoint
- [ ] Wire `PermissionCategory` to Permission entity as a value object
- [ ] Clean up `IPermissionService` public interface and API once we are closer to production. At this point we will really know what we will need and what not to expose to the client.

## Questions/Suggestions
- Permission Category Questions should be generalized to Plugin or Survey Extensions e.g. expressions, calculations, etc.
- My Account - This should be "Authenticated" level, not permission-based
- No special permission needed for self-management e.g. change password, change email, etc. - AllowAnonymous() for these endpoints
- Shall we store the tenantId in the cache key or userId is sufficient?
- Review if the Cache Invalidation cannot be separated from the PermissionService?
- Discuss if we need hierarchical roles and permissions at this stage?