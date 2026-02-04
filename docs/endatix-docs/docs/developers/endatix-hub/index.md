---
sidebar_position: 1
title: Developer Documentation
description: Integrate and customize Endatix Hub for your applications
---

# Developer Documentation

Build, integrate, and extend Endatix Hub with our comprehensive developer tools and APIs.

## What You Can Build

### 🔧 **Integration & Deployment**
- Integrate Endatix Hub into existing web applications
- Deploy custom instances with Docker
- Set up multi-tenant environments
- Configure custom domains and SSL certificates

### 🎨 **Custom Development**
- Create custom question types and widgets
- Build custom React components
- Develop custom themes and styling
- Extend the platform with custom APIs

### 🔌 **API & Integrations**
- Use the REST API for CRUD operations
- Implement webhooks for real-time notifications
- Integrate with external systems and databases
- Build custom authentication and authorization

### 🚀 **Advanced Features**
- Implement custom form validation
- Create custom submission processors
- Build custom export formats
- Develop custom analytics and reporting

## Core Concepts

### Architecture Overview
Endatix Hub follows modern web architecture principles:

- **Frontend**: Next.js with React components and TypeScript
- **Backend API**: RESTful API for form and submission management
- **Database**: PostgreSQL with Prisma ORM
- **Authentication**: NextAuth.js with multiple providers
- **File Storage**: AWS S3 or compatible storage

### Key Components

#### Form Management
- **Form Schema**: JSON-based form definitions using SurveyJS format
- **Form Submissions**: Structured data storage with validation
- **Form Templates**: Reusable form configurations

#### Custom Question Types
- **Widget Development**: Create custom SurveyJS widgets
- **React Components**: Build custom UI components
- **Validation**: Custom validation rules and logic

#### Event System
- **Webhooks**: HTTP callbacks for external integrations
- **Event Handlers**: Subscribe to form submission events
- **Custom Workflows**: Build complex business logic

#### API Integration
- **REST API**: Full CRUD operations for forms and submissions
- **Authentication**: JWT-based authentication system
- **Authorization**: Role-based access control

## Development Workflow

### 1. Setup Development Environment
```bash
# Clone the repository
git clone https://github.com/endatix/endatix-hub.git
cd endatix-hub

# Install dependencies
npm install

# Set up environment
cp .env.example .env.local
# Edit .env.local with your configuration

# Run database migrations
npm run db:migrate

# Start development server
npm run dev
```

### 2. Create Custom Components
```typescript
// Example: Custom question type component
import React from 'react';
import { QuestionType } from '@endatix/hub-types';

interface CustomQuestionProps {
  question: QuestionType;
  value: any;
  onChange: (value: any) => void;
}

export const CustomQuestion: React.FC<CustomQuestionProps> = ({
  question,
  value,
  onChange
}) => {
  return (
    <div className="custom-question">
      <label>{question.title}</label>
      <input
        type="text"
        value={value || ''}
        onChange={(e) => onChange(e.target.value)}
      />
    </div>
  );
};
```

### 3. Implement API Integrations
```typescript
// Example: Custom API client
import { EndatixClient } from '@endatix/api-client';

const client = new EndatixClient({
  baseUrl: 'https://your-instance.endatix.com',
  apiToken: 'your-api-token'
});

// Create a form
const form = await client.forms.create({
  title: 'My Form',
  schema: { /* form schema */ }
});

// Submit data
const submission = await client.submissions.create(form.id, {
  data: { name: 'John Doe' }
});
```

## API Reference

### Authentication
All API endpoints require authentication. Include your API key in the request headers:

```http
Authorization: Bearer YOUR_API_KEY
```

### Core Endpoints

#### Forms
- `GET /api/forms` - List all forms
- `POST /api/forms` - Create a new form
- `GET /api/forms/{id}` - Get form details
- `PUT /api/forms/{id}` - Update form
- `DELETE /api/forms/{id}` - Delete form

#### Submissions
- `GET /api/forms/{id}/submissions` - List form submissions
- `POST /api/forms/{id}/submissions` - Submit form data
- `GET /api/submissions/{id}` - Get submission details
- `PUT /api/submissions/{id}` - Update submission

#### Webhooks
- `GET /api/webhooks` - List webhooks
- `POST /api/webhooks` - Create webhook
- `DELETE /api/webhooks/{id}` - Delete webhook

## Best Practices

### Security
- Always validate input data
- Use HTTPS in production
- Implement proper authentication
- Follow the principle of least privilege

### Performance
- Use React.memo for expensive components
- Implement proper caching strategies
- Optimize database queries
- Monitor application performance

### Code Quality
- Follow TypeScript best practices
- Write unit tests for components
- Use ESLint and Prettier
- Document your code

## Getting Help

- **Documentation**: Browse the guides below
- **GitHub Issues**: Report bugs and request features
- **Community**: Join our developer community
- **Support**: Contact our technical support team
