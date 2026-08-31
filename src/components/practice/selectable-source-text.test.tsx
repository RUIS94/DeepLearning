import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { SelectableSourceText } from "./selectable-source-text";

describe("SelectableSourceText", () => {
  it("renders one span per character", () => {
    render(<SelectableSourceText text="abc" />);
    expect(screen.getAllByText(/^[abc]$/)).toHaveLength(3);
  });

  it("reports [start, end) on mouseDown -> mouseUp drag, matching the backend's character-offset coordinate system", () => {
    const onSelectRange = vi.fn();
    render(<SelectableSourceText text="hello" onSelectRange={onSelectRange} />);
    const chars = screen.getAllByText(/^[a-z]$/);

    fireEvent.mouseDown(chars[1]!); // "e"
    fireEvent.mouseEnter(chars[3]!); // "l" (second one)
    fireEvent.mouseUp(chars[3]!);

    expect(onSelectRange).toHaveBeenCalledWith(1, 4);
  });

  it("does not fire onSelectRange when readOnly", () => {
    const onSelectRange = vi.fn();
    render(<SelectableSourceText text="hello" onSelectRange={onSelectRange} readOnly />);
    const chars = screen.getAllByText(/^[a-z]$/);

    fireEvent.mouseDown(chars[0]!);
    fireEvent.mouseUp(chars[2]!);

    expect(onSelectRange).not.toHaveBeenCalled();
  });

  it("selects a single character when mouseDown and mouseUp land on the same char (no drag)", () => {
    const onSelectRange = vi.fn();
    render(<SelectableSourceText text="hello" onSelectRange={onSelectRange} />);
    const chars = screen.getAllByText(/^[a-z]$/);

    fireEvent.mouseDown(chars[0]!);
    fireEvent.mouseUp(chars[0]!);

    expect(onSelectRange).toHaveBeenCalledWith(0, 1);
  });
});
