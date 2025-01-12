import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { StorageService } from "../../infrastructure/storage-service";
import {
  BlobServiceClient,
  ContainerClient,
  BlockBlobClient,
} from "@azure/storage-blob";
import { optimizeImage } from "next/dist/server/image-optimizer";

vi.mock("@azure/storage-blob");
vi.mock("next/dist/server/image-optimizer");

describe("StorageService", () => {
  let service: StorageService;
  const mockAccountName = "mock-account-name";
  const mockAccountKey = "mock-account-key";
  const mockContainerName = "test-container";
  const mockFolderPath = "test-folder";
  const mockFileName = "test.jpg";
  const mockBuffer = Buffer.from("test");

  beforeEach(() => {
    vi.clearAllMocks();
    process.env.AZURE_STORAGE_ACCOUNT_NAME = mockAccountName;
    process.env.AZURE_STORAGE_ACCOUNT_KEY = mockAccountKey;
    service = new StorageService();
  });

  describe("constructor", () => {
    it("should throw error when account name is not set", () => {
      process.env.AZURE_STORAGE_ACCOUNT_NAME = "";
      expect(() => new StorageService()).toThrow(
        "Azure storage is not enabled"
      );
    });

    it("should throw error when account key is not set", () => {
      process.env.AZURE_STORAGE_ACCOUNT_KEY = "";
      expect(() => new StorageService()).toThrow(
        "Azure storage is not enabled"
      );
    });
  });

  describe("optimizeImageSize", () => {
    beforeEach(() => {
      process.env.RESIZE_IMAGES = "true";
      process.env.RESIZE_IMAGES_WIDTH = "800";
    });

    it("should optimize image when RESIZE_IMAGES is true", async () => {
      // Arrange
      const mockOptimizedBuffer = Buffer.from("optimized");
      vi.mocked(optimizeImage).mockResolvedValue(mockOptimizedBuffer);

      // Act
      const result = await service.optimizeImageSize(mockBuffer, "image/jpeg");

      // Assert
      expect(optimizeImage).toHaveBeenCalledWith({
        buffer: mockBuffer,
        contentType: "image/jpeg",
        quality: 80,
        width: 800,
      });
      expect(result).toBe(mockOptimizedBuffer);
    });

    it("should return original buffer when RESIZE_IMAGES is false", async () => {
      // Arrange
      process.env.RESIZE_IMAGES = "false";

      // Act
      const result = await service.optimizeImageSize(mockBuffer, "image/jpeg");

      // Assert
      expect(optimizeImage).not.toHaveBeenCalled();
      expect(result).toBe(mockBuffer);
    });

    it("should throw error when contentType is missing", async () => {
      // Act & Assert
      await expect(service.optimizeImageSize(mockBuffer, "")).rejects.toThrow(
        "contentType is not provided"
      );
    });
  });

  describe("uploadToStorage", () => {
    let mockBlobClient: BlockBlobClient;
    let mockContainerClient: ContainerClient;

    beforeEach(() => {
      mockBlobClient = {
        uploadData: vi.fn().mockResolvedValue(undefined),
        url: "https://test.blob.core.windows.net/test",
      } as unknown as BlockBlobClient;

      mockContainerClient = {
        createIfNotExists: vi.fn().mockResolvedValue(undefined),
        getBlockBlobClient: vi.fn().mockReturnValue(mockBlobClient),
      } as unknown as ContainerClient;

      vi.mocked(BlobServiceClient).mockImplementation(
        () =>
          ({
            getContainerClient: vi.fn().mockReturnValue(mockContainerClient),
          } as unknown as BlobServiceClient)
      );
    });

    afterEach(() => {
      vi.clearAllMocks();
    });

    it("should successfully upload file to blob storage", async () => {
      // Arrange
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      (service as any).blobServiceClient = {
        getContainerClient: vi.fn().mockReturnValue(mockContainerClient),
      } as unknown as BlobServiceClient;

      // Act
      const result = await service.uploadToStorage(
        mockBuffer,
        mockFolderPath,
        mockFileName,
        mockContainerName
      );

      // Assert
      expect(mockContainerClient.createIfNotExists).toHaveBeenCalledWith({
        access: "container",
      });
      expect(mockContainerClient.getBlockBlobClient).toHaveBeenCalledWith(
        `${mockFolderPath}/${mockFileName}`
      );
      expect(mockBlobClient.uploadData).toHaveBeenCalledWith(mockBuffer);
      expect(result).toBe(mockBlobClient.url);
    });

    it("should throw error when file buffer is not provided", async () => {
      // Act & Assert
      await expect(
        service.uploadToStorage(
          undefined as unknown as Buffer,
          mockFolderPath,
          mockFileName,
          mockContainerName
        )
      ).rejects.toThrow("a file is not provided");
    });

    it("should throw error when fileName is not provided", async () => {
      // Act & Assert
      await expect(
        service.uploadToStorage(
          mockBuffer,
          mockFolderPath,
          "",
          mockContainerName
        )
      ).rejects.toThrow("fileName is not provided");
    });

    it("should throw error when containerName is not provided", async () => {
      // Act & Assert
      await expect(
        service.uploadToStorage(mockBuffer, mockFolderPath, mockFileName, "")
      ).rejects.toThrow("container name is not provided");
    });
  });
});
