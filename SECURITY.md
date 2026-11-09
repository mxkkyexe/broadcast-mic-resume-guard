# Security

The guard runs as the interactive user and has no application network functionality. A compromised executable or writable installation can execute code with that user's permissions at login. Protect the installation folder and inspect release checksums or build from source.

Do not publish credentials or sensitive logs in issues. If the repository owner has enabled GitHub private vulnerability reporting, use that for security reports; otherwise open a minimal issue asking for a private reporting channel without exploit details.

Mute safety is best effort: the program never clears mute, but Windows exposes no atomic volume/mute transaction. See the README for the intentional-zero ambiguity and concurrent-change limitation.
