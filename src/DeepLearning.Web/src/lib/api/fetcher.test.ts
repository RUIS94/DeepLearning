import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError, createApiClient } from "./fetcher";

/**
 * `createApiClient` is the one place a request gets its `Authorization` header, its query
 * string, and its error translation — including the server-side client that (as of the C1
 * session round) now injects the logged-in user's JWT so SSR reads aren't anonymous. These
 * tests pin that behaviour against a stubbed `fetch`, no network.
 */
describe("createApiClient", () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    fetchMock.mockReset();
    vi.stubGlobal("fetch", fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  function jsonResponse(body: unknown, init?: { status?: number; ok?: boolean }) {
    return {
      ok: init?.ok ?? true,
      status: init?.status ?? 200,
      json: async () => body,
    } as Response;
  }

  it("attaches the Authorization header from getAuthHeader", async () => {
    fetchMock.mockResolvedValue(jsonResponse({ id: "1" }));
    const api = createApiClient("https://api.test/api/v1", async () => "Bearer abc.def");

    await api("/questions");

    const [, init] = fetchMock.mock.calls[0]!;
    expect((init.headers as Record<string, string>)["Authorization"]).toBe("Bearer abc.def");
  });

  it("omits Authorization when getAuthHeader returns null", async () => {
    fetchMock.mockResolvedValue(jsonResponse({ id: "1" }));
    const api = createApiClient("https://api.test/api/v1", async () => null);

    await api("/questions");

    const [, init] = fetchMock.mock.calls[0]!;
    expect((init.headers as Record<string, string>)["Authorization"]).toBeUndefined();
  });

  it("omits Authorization when no getAuthHeader is provided", async () => {
    fetchMock.mockResolvedValue(jsonResponse({ id: "1" }));
    const api = createApiClient("https://api.test/api/v1");

    await api("/questions");

    const [, init] = fetchMock.mock.calls[0]!;
    expect((init.headers as Record<string, string>)["Authorization"]).toBeUndefined();
  });

  it("serialises query params and drops undefined ones", async () => {
    fetchMock.mockResolvedValue(jsonResponse([]));
    const api = createApiClient("https://api.test/api/v1");

    await api("/questions", { query: { userId: "u-1", taskType: 0, skip: undefined } });

    const [url] = fetchMock.mock.calls[0]!;
    const search = new URL(String(url)).searchParams;
    expect(search.get("userId")).toBe("u-1");
    expect(search.get("taskType")).toBe("0");
    expect(search.has("skip")).toBe(false);
  });

  it("throws an ApiError carrying the parsed problem details on a non-ok response", async () => {
    fetchMock.mockResolvedValue(
      jsonResponse({ title: "Category has children", status: 409 }, { ok: false, status: 409 }),
    );
    const api = createApiClient("https://api.test/api/v1");

    const err = (await api("/question-bank-categories/x", { method: "DELETE" }).catch(
      (e) => e,
    )) as ApiError;

    expect(err).toBeInstanceOf(ApiError);
    expect(err.status).toBe(409);
    expect(err.problem?.title).toBe("Category has children");
    expect(err.message).toBe("Category has children");
  });

  it("returns undefined for a 204 without trying to parse a body", async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      status: 204,
      json: async () => {
        throw new Error("must not parse a 204 body");
      },
    } as unknown as Response);
    const api = createApiClient("https://api.test/api/v1");

    await expect(api("/some/mutation", { method: "POST" })).resolves.toBeUndefined();
  });
});
