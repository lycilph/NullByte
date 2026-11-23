Implementation plan

We'll do this incrementally:

1. Introduce the settings models
2. Add command-line parsing
3. Add settings.json loading
4. Merge defaults + JSON + CLI
5. Add centralized validation
6. Refactor Program.cs to consume AppSettings
7. Remove GetOption and GetSaveDirectory
8. Add tests for parsing, merging and validation
9. Update the design document

I don't think we need Microsoft.Extensions.Configuration here. For this small, known configuration surface, 
System.Text.Json plus our own small parser gives us much clearer behavior around precedence, unknown CLI arguments, 
validation, and error messages.