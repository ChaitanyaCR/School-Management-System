#!/usr/bin/env bash
# Scaffolds a business module from templates/module and wires it into the solution.
# Usage: scripts/new-module.sh Students
set -euo pipefail

name="${1:?Usage: $0 <ModuleName in PascalCase, e.g. Students>}"
if [[ ! "$name" =~ ^[A-Z][A-Za-z]+$ ]]; then
  echo "Module name must be PascalCase letters only (e.g. Students)." >&2
  exit 1
fi

cd "$(dirname "$0")/.."
module_dir="src/Modules/$name"
if [[ -e "$module_dir" ]]; then
  echo "$module_dir already exists." >&2
  exit 1
fi

dotnet new install ./templates/module --force > /dev/null
dotnet new schoolms-module --name "$name" --output . > /dev/null

dotnet sln SchoolMs.slnx add \
  "$module_dir/SchoolMs.Modules.$name/SchoolMs.Modules.$name.csproj" \
  "$module_dir/SchoolMs.Modules.$name.Contracts/SchoolMs.Modules.$name.Contracts.csproj" \
  "tests/SchoolMs.Modules.$name.UnitTests/SchoolMs.Modules.$name.UnitTests.csproj" > /dev/null
dotnet add src/SchoolMs.Api/SchoolMs.Api.csproj reference \
  "$module_dir/SchoolMs.Modules.$name/SchoolMs.Modules.$name.csproj" > /dev/null

echo "Created module '$name'. Next: register 'new ${name}Module()' in src/SchoolMs.Api/AppModules.cs."
