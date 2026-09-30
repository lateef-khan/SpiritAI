// Universal identifiers of the standard objects the page needs. They are
// literals because front-component bundles replace `twenty-sdk/define` with an
// empty stub, so STANDARD_OBJECT cannot be read at run time in the page. A
// unit test pins them to STANDARD_OBJECT.
export const STANDARD_OBJECT_UNIVERSAL_IDENTIFIERS = {
  company: '20202020-b374-4779-a561-80086cb2e17f',
  opportunity: '20202020-9549-49dd-b2b2-883999db8938',
  task: '20202020-1ba1-48ba-bc83-ef7e5990ed10',
  workspaceMember: '20202020-3319-4234-a34c-82d5c0e881a6',
} as const;
