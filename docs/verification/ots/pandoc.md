## Pandoc Verification

This document provides the verification evidence for the `Pandoc` OTS software item.

### Required Functionality

DemaConsulting.PandocTool converts Markdown source documents to HTML as part of the documentation
build pipeline. FileAssert validates that each generated HTML file exists, has a non-trivial size,
contains a valid HTML title element, and includes expected document content. Passing FileAssert
assertions for each document type proves Pandoc executed correctly and produced meaningful output.

### Verification Approach

Pandoc is verified by the Build Documents job in the CI pipeline. The job's
`Generate Build Notes HTML with Pandoc`, `Generate Code Quality HTML with Pandoc`,
`Generate Review Plan HTML with Pandoc`, `Generate Review Report HTML with Pandoc`,
`Generate Design HTML with Pandoc`, `Generate Verification HTML with Pandoc`, and
`Generate User Guide HTML with Pandoc` steps produce the corresponding HTML files under
`docs/*/generated/*.html`, and the matching FileAssert steps record that evidence in TRX artifacts
such as `artifacts/fileassert-build-notes.trx`, `artifacts/fileassert-code-quality.trx`,
`artifacts/fileassert-code-review.trx`, `artifacts/fileassert-design.trx`,
`artifacts/fileassert-verification.trx`, and `artifacts/fileassert-user-guide.trx`. A passing run
for those assertions constitutes evidence that the requirement is satisfied.

### Test Scenarios

#### Pandoc_BuildNotesHtml

**Scenario**: FileAssert asserts the build-notes HTML file exists, is non-trivially sized, contains
a valid HTML title element, and includes expected document content.

**Expected**: FileAssert exits 0 for the build-notes HTML document.

**Requirement coverage**: `Ste100Mark-OTS-Pandoc`.

#### Pandoc_CodeQualityHtml

**Scenario**: FileAssert asserts the code-quality HTML file exists, is non-trivially sized, contains
a valid HTML title element, and includes expected document content.

**Expected**: FileAssert exits 0 for the code-quality HTML document.

**Requirement coverage**: `Ste100Mark-OTS-Pandoc`.

#### Pandoc_ReviewPlanHtml

**Scenario**: FileAssert asserts the review plan HTML file exists, is non-trivially sized, contains
a valid HTML title element, and includes expected document content.

**Expected**: FileAssert exits 0 for the review plan HTML document.

**Requirement coverage**: `Ste100Mark-OTS-Pandoc`.

#### Pandoc_ReviewReportHtml

**Scenario**: FileAssert asserts the review report HTML file exists, is non-trivially sized,
contains a valid HTML title element, and includes expected document content.

**Expected**: FileAssert exits 0 for the review report HTML document.

**Requirement coverage**: `Ste100Mark-OTS-Pandoc`.

#### Pandoc_DesignHtml

**Scenario**: FileAssert asserts the design document HTML file exists, is non-trivially sized,
contains a valid HTML title element, and includes expected document content.

**Expected**: FileAssert exits 0 for the design document HTML.

**Requirement coverage**: `Ste100Mark-OTS-Pandoc`.

#### Pandoc_VerificationHtml

**Scenario**: FileAssert asserts the verification HTML file exists, is non-trivially sized, contains
a valid HTML title element, and includes expected verification document content.

**Expected**: FileAssert exits 0 for the verification document.

**Requirement coverage**: `Ste100Mark-OTS-Pandoc`.

#### Pandoc_UserGuideHtml

**Scenario**: FileAssert asserts the user guide HTML file exists, is non-trivially sized, contains
a valid HTML title element, and includes expected document content.

**Expected**: FileAssert exits 0 for the user guide HTML document.

**Requirement coverage**: `Ste100Mark-OTS-Pandoc`.
