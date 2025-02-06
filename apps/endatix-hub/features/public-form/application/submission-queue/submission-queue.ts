import { Result } from "@/lib/result";
import {
  SubmissionData,
  submitFormAction,
} from "../actions/submit-form.action";

interface QueueItem {
  formId: string;
  data: SubmissionData;
}

export class SubmissionQueue {
  private queue: QueueItem[] = [];
  private isProcessing = false;

  private async processQueue() {
    if (this.isProcessing || this.queue.length === 0) return;

    this.isProcessing = true;
    try {
      const itemToProcess = this.queue.shift();
      if (!itemToProcess) return;

      const result = await submitFormAction(
        itemToProcess.formId,
        itemToProcess.data,
      );

      if (Result.isError(result)) {
        console.debug("Failed to submit form", result.message);
      }
    } catch (error) {
      console.debug("Error processing partial submission:", error);
    } finally {
      this.isProcessing = false;
      if (this.queue.length > 0) {
        this.processQueue();
      }
    }
  }

  public enqueue(item: QueueItem): void {
    if (!item.formId || !item.data) {
      console.debug("Invalid queue item:", item);
      return;
    }

    this.queue.push(item);
    this.processQueue();
  }

  public clear(): void {
    this.queue = [];
  }

  public get processing(): boolean {
    return this.isProcessing;
  }

  public get queueLength(): number {
    return this.queue.length;
  }
}

export const submissionQueue = new SubmissionQueue();
