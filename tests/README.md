# Testing Guidelines

## Generating Unit Tests with Cursor

This repository includes a standardized prompt for generating unit tests using Cursor AI. The prompt helps create consistent unit tests using XUnit, NSubstitute, and FluentAssertions.

### How to Use

1. Open the source file containing the class you want to test in Cursor
2. Press `Ctrl+L` to open the chat panel
3. Copy the prompt below and replace the placeholders:
   - Replace `[CLASS_PLACEHOLDER]` with the name of the class you want to test
   - Replace `[NAMESPACE_PLACEHOLDER]` with the target namespace for the test file
4. Paste the modified prompt into the chat
5. Press `Ctrl+Enter` to execute the prompt against the entire codebase context
6. The generated tests will be displayed in the chat panel
7. Copy the generated tests and paste them into a new file in the corresponding tests folder
8. Run the tests to see if they pass and adjust them if necessary
9. Play with the prompt to see if you can get better tests, update it and share insights with the team. Have fun with AI tests generation!

### The Prompt
```
Please create unit tests for class [CLASS_PLACEHOLDER] using Xunit, NSubstitute and FluentAssertions, following the rules below.
- Prepare the test code ready to be stored in a file with all the neccesary using directives.
- The namespace of the file must be defined in file-scoped style, with a semicolon after the namespace name instead of a block under it.
- The namespace is [NAMESPACE_PLACEHOLDER].
- The tests names must follow the convention MethodName_StateUnderTest_ExpectedBehavior.
- The tests order should start from the failing to the successful.
- When the result of a mock method call is used, do not check it for a received call.
- When a mock method does not return a result or the result of its call is not used, do not set it to return a value and instead check it for a received call with the correct parameters.
- Do not assert the types of result objects when it is clear what the type is from the method signature.
- Before asserting the properties of an object, assert the object is not null.
- Assert errors coming from Guard.Against clauses using the method ErrorMessages.GetErrorMessage with the field name and error type from enum ErrorType.
- When sample form definition JSON data is needed, pick from the constants in class SampleData.
- Do not directly pass literals as agruments to method calls but before calling a method arrange variables with the values and pass the variables as arguments.
- Asserting excaptions from guard clauses must be done as e.g. in Endatix.Core.Tests.UseCases.FormDefinitions.Create.CreateFormDefinitionCommandTests - using Endatix.Core.Tests.ErrorMessages.GetErrorMessage and Endatix.Core.Tests.ErrorType.
- Use 'var' instead of explicit type when possible.
```

This prompt will generate unit tests following our project's conventions and best practices, including proper namespace structure, naming conventions, and assertion patterns.
