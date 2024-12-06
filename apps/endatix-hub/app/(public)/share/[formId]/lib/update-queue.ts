import { Result } from "@/lib/result";
import { SubmissionData, submitFormAction } from "../submit-form.action";

interface QueueItem {
    formId: string;
    data: SubmissionData;
}

export class UpdateQueue {
    private queue: QueueItem[] = [];
    private isProcessing = false;

    private processQueue = async () => {
        if (this.isProcessing || this.queue.length === 0) return;
        this.isProcessing = true;
        try {
            const itemToProcess = this.queue.shift();
            if (!itemToProcess) return;

            var result = await submitFormAction(itemToProcess.formId, itemToProcess.data);

            if (Result.isError(result)) {
                console.debug('Failed to submit form', result.message);
            }

        } catch (error) {
            console.debug('Error processing partial submission:', error);
        } finally {
            this.isProcessing = false;
            if (this.queue.length > 0) {
                this.processQueue();
            }
        }
    }

    enqueue(item: QueueItem) {
        this.queue.push(item);
        this.processQueue();
    }

    clear() {
        this.queue = [];
    }
}

export const updateQueue = new UpdateQueue();