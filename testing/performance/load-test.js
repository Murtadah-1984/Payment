import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';
import { htmlReport } from 'https://raw.githubusercontent.com/benc-uk/k6-reporter/main/dist/bundle.js';

// Custom metrics
const errorRate = new Rate('errors');
const paymentCreationTime = new Trend('payment_creation_time');
const paymentQueryTime = new Trend('payment_query_time');
const paymentCreationCounter = new Counter('payments_created');
const paymentQueryCounter = new Counter('payments_queried');

// Test configuration
export const options = {
  stages: [
    { duration: '30s', target: 10 },   // Ramp up to 10 users
    { duration: '1m', target: 50 },   // Ramp up to 50 users
    { duration: '2m', target: 100 },  // Ramp up to 100 users
    { duration: '3m', target: 100 }, // Stay at 100 users
    { duration: '1m', target: 200 },  // Spike to 200 users
    { duration: '2m', target: 200 }, // Stay at 200 users
    { duration: '1m', target: 50 },  // Ramp down to 50 users
    { duration: '30s', target: 0 },  // Ramp down to 0 users
  ],
  thresholds: {
    http_req_duration: ['p(95)<2000', 'p(99)<5000'], // 95% of requests < 2s, 99% < 5s
    http_req_failed: ['rate<0.01'],                   // Error rate < 1%
    errors: ['rate<0.01'],                            // Custom error rate < 1%
    payment_creation_time: ['p(95)<3000'],            // 95% of payment creations < 3s
    payment_query_time: ['p(95)<500'],                // 95% of payment queries < 500ms
  },
};

// Base URL - can be overridden via environment variable
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const API_VERSION = __ENV.API_VERSION || 'v1';
const JWT_TOKEN = __ENV.JWT_TOKEN || '';

// Helper function to generate random payment data
function generatePaymentData() {
  const providers = ['ZainCash', 'Stripe', 'FIB', 'Square'];
  const currencies = ['USD', 'EUR', 'IQD', 'GBP'];
  const paymentMethods = ['Card', 'BankTransfer', 'MobileWallet'];
  
  return {
    amount: Math.floor(Math.random() * 10000) + 100, // 100-10100
    currency: currencies[Math.floor(Math.random() * currencies.length)],
    paymentMethod: paymentMethods[Math.floor(Math.random() * paymentMethods.length)],
    provider: providers[Math.floor(Math.random() * providers.length)],
    merchantId: `merchant-${Math.floor(Math.random() * 1000)}`,
    orderId: `order-${Date.now()}-${Math.floor(Math.random() * 10000)}`,
    idempotencyKey: `idempotency-${Date.now()}-${Math.floor(Math.random() * 10000)}`,
  };
}

// Helper function to create JWT headers
function getHeaders() {
  const headers = {
    'Content-Type': 'application/json',
    'Accept': 'application/json',
  };
  
  if (JWT_TOKEN) {
    headers['Authorization'] = `Bearer ${JWT_TOKEN}`;
  }
  
  return headers;
}

// Test scenario: Create Payment
export function createPayment() {
  const paymentData = generatePaymentData();
  const url = `${BASE_URL}/api/${API_VERSION}/payments`;
  
  const startTime = Date.now();
  const response = http.post(url, JSON.stringify(paymentData), {
    headers: getHeaders(),
    tags: { name: 'CreatePayment' },
  });
  
  const duration = Date.now() - startTime;
  paymentCreationTime.add(duration);
  
  const success = check(response, {
    'payment creation status is 201': (r) => r.status === 201,
    'payment creation has payment ID': (r) => {
      if (r.status === 201) {
        const body = JSON.parse(r.body);
        return body.id !== undefined;
      }
      return false;
    },
  });
  
  if (!success) {
    errorRate.add(1);
  } else {
    paymentCreationCounter.add(1);
    return JSON.parse(response.body).id;
  }
  
  return null;
}

// Test scenario: Get Payment by ID
export function getPaymentById(paymentId) {
  if (!paymentId) return;
  
  const url = `${BASE_URL}/api/${API_VERSION}/payments/${paymentId}`;
  
  const startTime = Date.now();
  const response = http.get(url, {
    headers: getHeaders(),
    tags: { name: 'GetPaymentById' },
  });
  
  const duration = Date.now() - startTime;
  paymentQueryTime.add(duration);
  
  const success = check(response, {
    'payment query status is 200': (r) => r.status === 200,
    'payment query has payment data': (r) => {
      if (r.status === 200) {
        const body = JSON.parse(r.body);
        return body.id === paymentId;
      }
      return false;
    },
  });
  
  if (!success) {
    errorRate.add(1);
  } else {
    paymentQueryCounter.add(1);
  }
}

// Test scenario: List Payments
export function listPayments() {
  const url = `${BASE_URL}/api/${API_VERSION}/payments?pageNumber=1&pageSize=10`;
  
  const response = http.get(url, {
    headers: getHeaders(),
    tags: { name: 'ListPayments' },
  });
  
  check(response, {
    'list payments status is 200': (r) => r.status === 200,
    'list payments returns array': (r) => {
      if (r.status === 200) {
        const body = JSON.parse(r.body);
        return Array.isArray(body.items) || Array.isArray(body);
      }
      return false;
    },
  });
}

// Test scenario: Health Check
export function healthCheck() {
  const url = `${BASE_URL}/health`;
  
  const response = http.get(url, {
    tags: { name: 'HealthCheck' },
  });
  
  check(response, {
    'health check status is 200': (r) => r.status === 200,
  });
}

// Main test function
export default function () {
  // Health check (lightweight)
  healthCheck();
  sleep(0.5);
  
  // Create payment (main workload)
  const paymentId = createPayment();
  sleep(1);
  
  // Query payment if creation was successful
  if (paymentId) {
    getPaymentById(paymentId);
    sleep(0.5);
  }
  
  // List payments (less frequent)
  if (Math.random() > 0.7) {
    listPayments();
    sleep(0.5);
  }
}

// Generate HTML report
export function handleSummary(data) {
  return {
    'summary.html': htmlReport(data),
    'stdout': JSON.stringify(data),
  };
}

