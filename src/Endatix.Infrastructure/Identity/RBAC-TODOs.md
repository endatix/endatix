# RBAC TODOs

This is created as part of the [Role Based Access Control (RBAC) #261](https://github.com/endatix/endatix-private/issues/261) to track TODOs pending completion as we get ready to merge the RBAC changes into the `main` branch.

## TODOs

- [ ] Wire `PermissionCategory` to Permission entity as a value object
- [ ] Fix migration issue with AppRoleId column
- [ ] Clean up `IPermissionService` public interface and API once we are closer to production. At this point we will really know what we will need and what not to expose to the client.

## Questions/Suggestions
- Permission Category Questions should be generalized to Plugin or Survey Extensions e.g. expressions, calculations, etc.
- My Account - This should be "Authenticated" level, not permission-based
- No special permission needed for self-management e.g. change password, change email, etc. - AllowAnonymous() for these endpoints
- Shall we store the tenantId in the cache key or userId is sufficient?
- Review if the Cache Invalidation cannot be separated from the PermissionService?
- Discuss if we need hierarchical roles and permissions at this stage?
- IsAdmin - what's the logic we need to implement as well as the scope?
- `EnrichWithUserPermissionsAsync` - do we need to add a claim for "IsViewer" e.g. fast frontend acccess. Also let's review strategy to add all vs selected permission sets to the ClaimsIdentity
