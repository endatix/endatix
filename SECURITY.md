# Security Policy

Endatix API is **open-source software** (MIT License) maintained by Endatix, Ltd. and its community.

We take the security of Endatix seriously. If you believe you have found a vulnerability, please report it to us privately so we can investigate and remediate it before any public disclosure.

## Supported Versions

| Version | Supported |
| :--- | :--- |
| Latest stable release | ✅ |
| `main` branch (pre-release / development) | ✅ *(best effort)* |
| Older releases | ❌ *(Please upgrade; critical fixes may be backported at our discretion)* |

Security fixes are published for the latest supported release line. Self-hosters and downstream integrators should apply updates promptly.

## Reporting a Vulnerability

**Do not** open a public GitHub issue, discussion, or pull request for security vulnerabilities.

### Preferred channel: GitHub Private Vulnerability Reporting (PVR)

Use GitHub’s private vulnerability reporting so the report stays confidential until a fix is available:

1. Open the repository's **Security** tab, or go directly to [Report a vulnerability](https://github.com/endatix/endatix/security/advisories/new)
2. Provide as much detail as you can (see below)
3. Submit the report and wait for our acknowledgment

This is the **preferred** way for users, customers, partners, and researchers to disclose potential security issues in Endatix API.

### What to include

Helpful reports typically include:

*   A clear description of the issue and its security impact.
*   Affected endpoint, package, version, and runtime context (.NET version, OS, etc.) when known.
*   **Authentication/Authorization Context:** Explicitly state if the exploit requires authentication, specific API scopes/roles, or if it can be triggered by unauthenticated traffic.
*   Steps to reproduce, or a proof of concept.
*   Any logs, screenshots, or request/response samples (redact secrets and personal data).
*   Your assessment of severity *(optional; we will triage independently)*.

### Response expectations

We aim to:

*   **Acknowledge** valid reports within **3 business days**.
*   **Triage** and share an initial assessment within **10 business days**.
*   Keep you informed of remediation progress when appropriate.
*   Credit reporters in advisories or release notes when a fix is published (unless you prefer to remain anonymous).

Complex issues may take longer; we will communicate timelines as we learn more.

## Coordinated Disclosure

Please give us a reasonable opportunity to investigate and ship a fix before any public disclosure.

We ask that you:

*   Do not exploit the issue beyond what is needed to demonstrate it.
*   Do not access, modify, or delete data that is not yours.
*   Do not publicly share exploit details, advisories, or blog posts until we confirm a fix is available (or we agree otherwise in writing).

We will work with you on a coordinated disclosure timeline once remediation is planned. Where appropriate, we may publish a GitHub Security Advisory (GHSA) and request a CVE.

<details>
<summary><strong>Out of Scope</strong></summary>

The following are generally **out of scope** unless they demonstrate a practical security impact on Endatix API:

*   Issues in third-party dependencies with no demonstrated exploitability in this project (report upstream when appropriate).
*   Social engineering, phishing, or physical attacks.
*   Denial-of-service from excessive legitimate traffic without a specific application flaw.
*   Reports based solely on automated scanner output without a validated reproduction path.
*   Missing security headers or best-practice recommendations without a concrete vulnerability.
*   Vulnerabilities only present in unsupported or heavily customized forks.
</details>

<details>
<summary><strong>Safe Harbor</strong></summary>

If you research and report vulnerabilities in good faith, following this policy, we will not pursue legal action related to that research. Good faith means staying within the guidelines above and avoiding privacy violations, service disruption, or data destruction.
</details>

---

## Contact

*   **Preferred:** [GitHub Private Vulnerability Reporting](https://github.com/endatix/endatix/security/advisories/new)
*   **General inquiries:** [tech@endatix.com](mailto:tech@endatix.com) *(not for confidential vulnerability details when PVR is available)*

Thank you for helping keep Endatix and the open-source community secure.