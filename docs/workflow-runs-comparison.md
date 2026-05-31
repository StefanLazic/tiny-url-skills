# Workflow Runs Comparison: Run #81 vs Run #80

Comparison of the two latest workflow runs for the `Running Copilot cloud agent` workflow on branch `copilot/stress-tool-requests-per-second`.

## Summary

| Attribute | Run #81 | Run #80 |
|-----------|---------|---------|
| **Run ID** | 26705165931 | 26705142317 |
| **Run Number** | 81 | 80 |
| **Status** | In Progress | Completed |
| **Conclusion** | N/A (still running) | Cancelled |
| **Event** | dynamic | dynamic |
| **Branch** | copilot/stress-tool-requests-per-second | copilot/stress-tool-requests-per-second |
| **Commit SHA** | c08e8dcb90a088ed0f2156300c18f5e73097974d | c08e8dcb90a088ed0f2156300c18f5e73097974d |
| **Created At** | 2026-05-31T06:16:58Z | 2026-05-31T06:15:39Z |
| **Updated At** | 2026-05-31T06:17:03Z | 2026-05-31T06:16:37Z |
| **Runner** | GitHub Actions 1000000207 | GitHub Actions 1000000206 |
| **Labels** | ubuntu-latest | ubuntu-latest |

## Job Steps Comparison

Both runs execute the same `copilot` job with identical steps. Below is the step-by-step comparison:

| # | Step Name | Run #81 | Run #80 |
|---|-----------|---------|---------|
| 1 | Set up job | ✅ success | ✅ success |
| 2 | Initialize | ✅ success | ✅ success |
| 3 | Validate runner OS | ⏭️ skipped | ⏭️ skipped |
| 4 | Validate firewall settings (Linux) | ⏭️ skipped | ⏭️ skipped |
| 5 | Validate firewall settings (Windows) | ⏭️ skipped | ⏭️ skipped |
| 6 | Pre-cache Playwright MCP server (Linux) | ✅ success | ✅ success |
| 7 | Pre-cache Playwright MCP server (Windows) | ⏭️ skipped | ⏭️ skipped |
| 8 | Prepare Copilot (Linux) | ✅ success (14s) | ✅ success (16s) |
| 9 | Prepare Copilot (Windows) | ⏭️ skipped | ⏭️ skipped |
| 10 | Download Autofind (Linux) | ✅ success | ✅ success |
| 11 | Download Autofind (Windows) | ⏭️ skipped | ⏭️ skipped |
| 12 | Generate agent firewall CA (Linux) | ✅ success | ✅ success |
| 13 | Start MCP Servers (Linux) | ✅ success | ✅ success |
| 14 | Start MCP Servers (Windows) | ⏭️ skipped | ⏭️ skipped |
| 15 | Processing Request (Linux) | 🔄 in progress | ❌ cancelled (29s) |
| 16 | Processing Request (Windows) | ⏳ pending | ⏭️ skipped |
| 17 | Clean Up (Linux) | ⏳ pending | ✅ success |
| 18 | Clean Up (Windows) | ⏳ pending | ⏭️ skipped |
| 19 | [Optional] Archive Details | ⏳ pending | ⏭️ skipped |

## Key Differences

1. **Same commit**: Both runs execute against the same commit (`c08e8dc`), indicating Run #81 is a retry of Run #80.
2. **Run #80 was cancelled**: The previous run was cancelled during the "Processing Request (Linux)" step after running for ~29 seconds.
3. **Run #81 is in progress**: The current run has progressed past all setup steps and is actively processing the request.
4. **Timing**: Run #81 started ~1 minute and 19 seconds after Run #80, suggesting it was triggered as a replacement after the cancellation.
5. **Different runners**: Each run was assigned to a different GitHub Actions runner instance (1000000207 vs 1000000206).

## Timing Comparison

| Phase | Run #81 | Run #80 |
|-------|---------|---------|
| Total duration | Still running | ~58s (cancelled) |
| Setup to Processing start | ~23s | ~21s |
| Prepare Copilot (Linux) | ~14s | ~16s |
| Processing Request (Linux) | In progress | ~29s (cancelled) |

---

*Report generated: 2026-05-31T06:17:34Z*
