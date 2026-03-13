export const manifests: Array<UmbExtensionManifest> = [
  {
    name: "Jobs Jobs Jobs Entrypoint",
    alias: "JobsJobsJobs.Entrypoint",
    type: "backofficeEntryPoint",
    js: () => import("./entrypoint.js"),
  },
];
