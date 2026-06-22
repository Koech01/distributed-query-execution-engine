# Security Policy

## Supported versions

Security fixes are applied to the latest release on the default branch.

| Version | Supported |
|---|---|
| latest on `main` | yes |
| older releases | no |

## Reporting a vulnerability

Please report security vulnerabilities privately. Do not open a public GitHub issue.

Send details to the repository maintainers through GitHub Security Advisories or by contacting the project owners directly if advisory access is unavailable.

Include:

- Description of the vulnerability
- Steps to reproduce
- Impact assessment
- Suggested fix, if you have one

We aim to acknowledge reports within 5 business days.

## Sensitive data

When reporting issues or opening pull requests, never include:

- Production credentials or JWT signing keys
- Database connection strings with passwords
- OAuth client secrets
- Personal data from production systems

## Secure deployment reminders

- Configure stable RSA JWT keys in production (`Authentication__JwtSigning__*`).
- Disable development account seeding (`Authentication__Seed__Enabled=false`).
- Protect Redis, RabbitMQ, and Consul with authentication and network isolation.
- Terminate TLS at your load balancer or ingress.
- Restrict worker network access to required shard databases and coordinator gRPC traffic.
