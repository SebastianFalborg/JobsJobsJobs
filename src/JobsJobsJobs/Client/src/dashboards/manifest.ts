export const manifests: Array<any> = [
  {
    name: "Jobs Jobs Jobs Background Jobs Dashboard",
    alias: "JobsJobsJobs.Dashboard.BackgroundJobs",
    type: "dashboard",
    js: () => import("./background-jobs-dashboard.element.js"),
    meta: {
      label: "Background Jobs",
      pathname: "background-jobs",
    },
    conditions: [
      {
        alias: "Umb.Condition.SectionAlias",
        match: "Umb.Section.Settings",
      },
    ],
  },
];
