import type { ReactNode } from "react";
import { describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { z } from "zod";
import { CrudTable, type CrudColumn, type CrudField } from "./crud-table";
import { I18nProvider } from "@/lib/i18n";

interface Widget {
  id: string;
  name: string;
}

const schema = z.object({ name: z.string().trim().min(1, "名称不能为空") });
type FormInput = z.infer<typeof schema>;

const fields: CrudField<FormInput>[] = [{ name: "name", label: "名称", kind: "text" }];
const columns: CrudColumn<Widget>[] = [{ key: "name", header: "名称", render: (w) => w.name }];

/** CrudTable now reads the UI language via useI18n(); tests default to the "en" locale. */
function Wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return (
    <QueryClientProvider client={client}>
      <I18nProvider>{children}</I18nProvider>
    </QueryClientProvider>
  );
}

function setup(onCreate: (values: FormInput) => Promise<Widget>) {
  return render(
    <CrudTable
      columns={columns}
      items={[]}
      isLoading={false}
      getRowId={(w) => w.id}
      schema={schema}
      fields={fields}
      defaultValues={{ name: "" }}
      onCreate={onCreate}
      dialogTitle="New Widget"
    />,
    { wrapper: Wrapper },
  );
}

describe("CrudTable", () => {
  it("shows the empty-state message when there are no items", () => {
    setup(vi.fn());
    expect(screen.getByText("No data")).toBeInTheDocument();
  });

  it("blocks submission and shows the zod error when a required field is empty", async () => {
    const user = userEvent.setup();
    const onCreate = vi.fn();
    setup(onCreate);

    await user.click(screen.getByRole("button", { name: "Create" }));
    await user.click(screen.getByRole("button", { name: "Confirm" }));

    expect(await screen.findByText("名称不能为空")).toBeInTheDocument();
    expect(onCreate).not.toHaveBeenCalled();
  });

  it("calls onCreate with the validated values and closes the dialog on success", async () => {
    const user = userEvent.setup();
    const onCreate = vi.fn().mockResolvedValue({ id: "w-1", name: "第一个" });
    setup(onCreate);

    await user.click(screen.getByRole("button", { name: "Create" }));
    await user.type(screen.getByLabelText("名称"), "第一个");
    await user.click(screen.getByRole("button", { name: "Confirm" }));

    await waitFor(() => expect(onCreate).toHaveBeenCalledWith({ name: "第一个" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
  });

  it("surfaces a thrown error from onCreate instead of silently closing the dialog", async () => {
    const user = userEvent.setup();
    const onCreate = vi.fn().mockRejectedValue(new Error("服务器拒绝了这次创建"));
    setup(onCreate);

    await user.click(screen.getByRole("button", { name: "Create" }));
    await user.type(screen.getByLabelText("名称"), "第一个");
    await user.click(screen.getByRole("button", { name: "Confirm" }));

    expect(await screen.findByText("服务器拒绝了这次创建")).toBeInTheDocument();
    expect(screen.getByRole("dialog")).toBeInTheDocument();
  });
});
