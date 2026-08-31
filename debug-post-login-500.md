# Debug Session: post-login-500
- **Status**: [OPEN]
- **Issue**: 登录成功后，访问其他页面时 Network 中大部分接口返回 500。
- **Debug Server**: Pending
- **Log File**: .dbg/trae-debug-log-post-login-500.ndjson

## Reproduction Steps
1. 打开前端 `http://localhost:3000/`
2. 完成登录
3. 访问其他业务页面
4. 观察多数接口返回 `500`

## Hypotheses & Verification
| ID | Hypothesis | Likelihood | Effort | Evidence |
|----|------------|------------|--------|----------|
| A | 登录后 token 或用户上下文未被稳定传递，导致后续接口在鉴权/取当前用户时抛异常 | High | Low | Pending |
| B | 用户档案初始化链路依赖的用户数据不完整，触发空值或约束异常 | High | Medium | Pending |
| C | 登录后页面请求参数或路由拼接异常，后端未兜底并返回 500 | Medium | Low | Pending |
| D | 当前数据库结构与代码期望不一致，后续页面依赖表/列缺失 | Medium | Medium | Pending |
| E | 前端请求拦截器在登录后改写了 base URL 或 headers/cookie | Medium | Low | Pending |

## Log Evidence
- Pending

## Verification Conclusion
- Pending
