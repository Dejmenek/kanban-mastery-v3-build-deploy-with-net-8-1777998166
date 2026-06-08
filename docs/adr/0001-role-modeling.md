# Title
Role modeling for board members

# Date
2026-06-08

## Context
Board members need roles (e.g. Owner and Member). The way roles are modeled affects data integrity, flexibility, and what features we can support.

## Considered Options

### Role as a string property
If we only need a simple role system with a few predefined roles, we could model the role as a string property on the `BoardMembers` table.
We would need to enforce data integrity through application logic and database constraint to ensure that only valid roles are assigned to members.
This approach is straightforward but not flexible if we wanted to add more roles, let board owners define custom ones or define permissions.

### Role as an enum
If we only need a fixed set of roles (e.g. Owner and Member) with no additional properties, we could model the role as an enum.
This approach is simple and provides better data integrity than a string property, as the database can enforce that only valid enum values are used.
However, it still lacks flexibility, adding new roles would require database migrations, and it doesn't allow for custom roles per board or permissions.

### Seperate `BoardRoles` table and join table between `BoardMembers` and `BoardRoles`
If we needed to support more complex roles with additional properties or even custom roles per board, we could create a separate `BoardRoles` table to define the roles and a join table between `BoardMembers` and `BoardRoles` to assign roles to members.
This way, we can easily add new roles, define permissions, and even allow board owners to create custom roles. This approach provides the most flexibility and data integrity.

## Decision
Since I don't have exact project requirements, I will choose the second option, modeling the role as an enum, as it provides a good balance between simplicity and data integrity for a basic role system with predefined roles.
Also it matches with Sprint 1 requirements, which is to have a role property on the `BoardMembers`.
If I would know that we need custom roles, assigning multiple roles to a member or permissions, I would choose the third option, which is more flexible and scalable.