import { createBrowserApiClient } from "./fetcher";
import type {
  ActivateLlmProviderResult,
  AiOperationOverrideResultItem,
  AiOperationType,
  LlmProviderModel,
  LlmProviderSettings,
  SelectLlmProviderModelResult,
  SetAiOperationOverrideResult,
  UpdateLlmProviderSettingsRequest,
  UpdateLlmProviderSettingsResult,
} from "@/lib/types/dtos";

const api = createBrowserApiClient();

export async function listLlmProviderSettings(): Promise<LlmProviderSettings[]> {
  return api<LlmProviderSettings[]>("/llm-provider-settings");
}

export async function updateLlmProviderSettings(
  providerKey: string,
  patch: UpdateLlmProviderSettingsRequest,
): Promise<UpdateLlmProviderSettingsResult> {
  return api<UpdateLlmProviderSettingsResult>(`/llm-provider-settings/${providerKey}`, {
    method: "PATCH",
    body: patch,
  });
}

export async function activateLlmProvider(providerKey: string): Promise<ActivateLlmProviderResult> {
  return api<ActivateLlmProviderResult>(`/llm-provider-settings/${providerKey}/activate`, {
    method: "POST",
  });
}

export async function listLlmProviderModels(providerKey: string): Promise<LlmProviderModel[]> {
  return api<LlmProviderModel[]>(`/llm-provider-settings/${providerKey}/models`);
}

export async function addLlmProviderModel(
  providerKey: string,
  model: string,
  label?: string | null,
): Promise<LlmProviderModel> {
  return api<LlmProviderModel>(`/llm-provider-settings/${providerKey}/models`, {
    method: "POST",
    body: { model, label: label ?? null },
  });
}

export async function selectLlmProviderModel(
  providerKey: string,
  model: string,
): Promise<SelectLlmProviderModelResult> {
  return api<SelectLlmProviderModelResult>(
    `/llm-provider-settings/${providerKey}/models/${encodeURIComponent(model)}/select`,
    { method: "POST" },
  );
}

export async function listAiOperationOverrides(): Promise<AiOperationOverrideResultItem[]> {
  return api<AiOperationOverrideResultItem[]>("/llm-provider-settings/operation-overrides");
}

export async function setAiOperationOverride(
  operationType: AiOperationType,
  providerKey: string,
  model?: string | null,
  thinkingEnabled?: boolean | null,
  effort?: string | null,
): Promise<SetAiOperationOverrideResult> {
  return api<SetAiOperationOverrideResult>(
    `/llm-provider-settings/operation-overrides/${operationType}`,
    {
      method: "PUT",
      body: {
        providerKey,
        model: model ?? null,
        thinkingEnabled: thinkingEnabled ?? null,
        effort: effort ?? null,
      },
    },
  );
}

export async function clearAiOperationOverride(operationType: AiOperationType): Promise<void> {
  await api<void>(`/llm-provider-settings/operation-overrides/${operationType}`, {
    method: "DELETE",
  });
}
