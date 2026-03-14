const o = [
  {
    name: "Jobs Jobs Jobs Entrypoint",
    alias: "JobsJobsJobs.Entrypoint",
    type: "backofficeEntryPoint",
    js: () => import("./entrypoint-Cljvn-9A.js")
  }
], s = [
  {
    name: "Jobs Jobs Jobs Background Jobs Dashboard",
    alias: "JobsJobsJobs.Dashboard.BackgroundJobs",
    type: "dashboard",
    js: () => import("./background-jobs-dashboard.element-X0EZH2k0.js"),
    meta: {
      label: "Background Jobs",
      pathname: "background-jobs"
    },
    conditions: [
      {
        alias: "Umb.Condition.SectionAlias",
        match: "Umb.Section.Settings"
      }
    ]
  }
], a = [
  ...o,
  ...s
];
export {
  a as manifests
};
//# sourceMappingURL=jobs-jobs-jobs.js.map
