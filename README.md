<div align="center">

<!-- Header Banner -->
<img src="https://capsule-render.vercel.app/api?type=waving&color=gradient&customColorList=0,2,3,5,30&height=200&section=header&text=C%23%20.NET%20Enterprise%20Suite&fontSize=32&animation=twinkling&fontAlignY=35&desc=ENIAD%20Berkane%20%7C%20Enterprise%20Software%20Engineering%20Curriculum&descSize=14&descAlignY=55" alt="C# .NET Enterprise Architecture Suite Banner" width="100%" />

<!-- Typing Animation -->
<p align="center">
  <img src="https://readme-typing-svg.demolab.com?font=Fira+Code&weight=600&size=20&duration=3000&pause=1000&color=00D9FF&center=true&vCenter=true&repeat=true&width=800&height=40&lines=C%23%20and%20.NET%20Enterprise%20Architecture;Event-Driven%20Architecture%20%26%20Domain%20Logic;MSTest%20Automated%20Testing%20Harness;.NET%209%20High-Performance%20Engineering" alt="Typing SVG" />
</p>

<!-- Quality & Community Badges -->
<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square" alt="MIT License" /></a>
  <a href="https://github.com/Bosaj/csharp-dotnet-enterprise-suite/actions"><img src="https://img.shields.io/badge/CI%20Pipeline-Passing-brightgreen?style=flat-square&logo=githubactions" alt="CI Status" /></a>
  <a href="https://github.com/Bosaj/csharp-dotnet-enterprise-suite"><img src="https://img.shields.io/github/stars/Bosaj/csharp-dotnet-enterprise-suite?style=flat-square&logo=github&color=00d9ff" alt="Stars" /></a>
  <a href="https://github.com/users/Bosaj/projects"><img src="https://img.shields.io/badge/Project_Board-Project_36-blue?style=flat-square&logo=github" alt="Project Board" /></a>
  <a href="https://github.com/stars/Bosaj/lists/eniad-academic-projects"><img src="https://img.shields.io/badge/Curated_List-ENIAD_Academic_Projects-gold?style=flat-square&logo=github" alt="Curated List" /></a>
  <img src="https://img.shields.io/badge/Institution-ENIAD%20Berkane-FF6B00?style=flat-square" alt="ENIAD Berkane" />
  <img src="https://img.shields.io/badge/.NET-9.0-512BD4?style=flat-square&logo=dotnet&logoColor=white" alt=".NET 9.0" />
  <img src="https://img.shields.io/badge/C%23-13.0-239120?style=flat-square&logo=csharp&logoColor=white" alt="C# 13.0" />
</p>

</div>

<!-- Divider -->
<img src="https://user-images.githubusercontent.com/73097560/115834477-dbab4500-a447-11eb-908a-139a6edaec5c.gif" alt="Divider" width="100%" />

## 📖 Overview

**C# .NET Enterprise Suite** is an academic software engineering and systems suite developed within the **State Engineering Degree in Artificial Intelligence & Digital Systems** at the **École Nationale d'Intelligence Artificielle et du Digital (ENIAD)**, Mohammed First University, Berkane, Morocco.

Developed for the **Développement d'Applications .NET** module (Semestre 7), this repository demonstrates enterprise .NET software engineering patterns, modular solution design, robust exception handling, and automated unit testing harnesses under modern .NET 9.

---

## 🏗️ Technical Architecture

```mermaid
graph TD
    subgraph Solution_Architecture [EniadCsharpDotnet.sln]
        CLI[EniadCsharpDotnet - Console Entrypoint]
        Core[Domain Logic & Comparison Engine]
        Tests[MSTest Automated Test Suite]
    end

    CLI -->|Invokes| Core
    Tests -->|Validates Boundary Conditions| Core
    Core --> Output[Standard Output / Exit Codes]
```

---

## 🚀 Getting Started

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or compatible modern .NET runtime.

### Build & Run
```bash
# Clone the repository
git clone https://github.com/Bosaj/csharp-dotnet-enterprise-suite.git
cd csharp-dotnet-enterprise-suite

# Restore and build the solution
dotnet build EniadCsharpDotnet.sln

# Run the console entrypoint with default arguments (500 vs 600)
dotnet run --project CsharpProjects/EniadCsharpDotnet

# Run with custom integer parameters
dotnet run --project CsharpProjects/EniadCsharpDotnet -- 42 128
```

### Running Automated Tests
```bash
dotnet test tests/EniadCsharpDotnet.Tests/EniadCsharpDotnet.Tests.csproj --verbosity normal
```

---

## 📁 Repository Structure

```text
csharp-dotnet-enterprise-suite/
├── .devcontainer/
│   └── devcontainer.json
├── .github/
│   └── workflows/
│       └── ci.yml
├── assets/
│   └── social_preview.png
├── CsharpProjects/
│   └── EniadCsharpDotnet/
│       ├── Program.cs
│       └── EniadCsharpDotnet.csproj
├── docs/
│   └── wiki/
│       ├── Architecture-and-Design.md
│       ├── Developer-Guide.md
│       ├── Getting-Started.md
│       └── Home.md
├── tests/
│   └── EniadCsharpDotnet.Tests/
│       ├── ComparisonTests.cs
│       └── EniadCsharpDotnet.Tests.csproj
├── .editorconfig
├── CITATION.cff
├── CODE_OF_CONDUCT.md
├── CONTRIBUTING.md
├── Directory.Build.props
├── EniadCsharpDotnet.sln
├── LICENSE
├── README.md
└── SECURITY.md
```

---

## 📜 Academic Integrity & Citation

This repository is maintained as part of the official academic engineering portfolio of **Oussama EL HADJI** at **ENIAD Berkane**. If you reference this work, please see [`CITATION.cff`](CITATION.cff).

```bibtex
@misc{elhadji2026csharp,
  author = {EL HADJI, Oussama},
  title = {C# .NET Enterprise Architecture Suite},
  year = {2026},
  publisher = {GitHub},
  journal = {GitHub repository},
  howpublished = {\url{https://github.com/Bosaj/csharp-dotnet-enterprise-suite}}
}
```

---

## 🛡️ License & Security

- **License**: Released under the [MIT License](LICENSE).
- **Security Policy**: See [SECURITY.md](SECURITY.md) for vulnerability disclosure guidelines.
