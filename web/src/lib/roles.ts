export const RoleDoctor = "Doctor" as const;
export const RoleSecretary = "Secretary" as const;
export const RolePatient = "Patient" as const;

export type Role = typeof RoleDoctor | typeof RoleSecretary | typeof RolePatient;

/** Policies mirror: "ClinicalStaff" = Doctor + Secretary. */
export const ClinicalStaffRoles: readonly Role[] = [RoleDoctor, RoleSecretary] as const;

export function hasRole(roles: readonly string[] | undefined, required: Role): boolean {
  return roles?.includes(required) ?? false;
}

export function isClinicalStaff(roles: readonly string[] | undefined): boolean {
  return ClinicalStaffRoles.some((role) => hasRole(roles, role));
}

export function isPatient(roles: readonly string[] | undefined): boolean {
  return hasRole(roles, RolePatient);
}