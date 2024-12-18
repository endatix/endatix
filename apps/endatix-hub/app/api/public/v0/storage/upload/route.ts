export async function POST(request: Request) {
  try {
    const formData = await request.formData();
    const files: { name: string; data: string }[] = [];

    for (const [filename, file] of formData.entries()) {
      if (!(file instanceof File)) {
        return Response.json({ error: `Invalid file for ${filename}` }, { status: 400 });
      }

      const fileBuffer = Buffer.from(await file.arrayBuffer());
      files.push({
        name: filename,
        data: fileBuffer.toString('base64')
      });
    }

    if (files.length === 0) {
      return Response.json({ error: 'No files provided' }, { status: 400 });
    }

    return Response.json({ files: files.map(file => file.name) });
  } catch (error) {
    return Response.json({ error: 'Invalid request' }, { status: 400 });
  }
}
