import http from 'k6/http';
import { check, sleep } from 'k6';

// Spike test - sudden increase in load to test system resilience
export const options = {
  stages: [
    { duration: '1m', target: 100 },   // Normal load
    { duration: '10s', target: 1000 }, // Sudden spike to 1000 users
    { duration: '1m', target: 1000 },  // Stay at spike
    { duration: '10s', target: 100 },  // Sudden drop
    { duration: '1m', target: 100 },   // Normal load
  ],
  thresholds: {
    http_req_duration: ['p(95)<10000'], // Allow higher latency during spike
    http_req_failed: ['rate<0.10'],    // Allow up to 10% error rate during spike
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const API_VERSION = __ENV.API_VERSION || 'v1';
const JWT_TOKEN = __ENV.JWT_TOKEN || '';

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

function generatePaymentData() {
  return {
    amount: Math.floor(Math.random() * 10000) + 100,
    currency: 'USD',
    paymentMethod: 'Card',
    provider: 'ZainCash',
    merchantId: `merchant-${Math.floor(Math.random() * 1000)}`,
    orderId: `order-${Date.now()}-${Math.floor(Math.random() * 10000)}`,
    idempotencyKey: `idempotency-${Date.now()}-${Math.floor(Math.random() * 10000)}`,
  };
}

export default function () {
  const paymentData = generatePaymentData();
  const url = `${BASE_URL}/api/${API_VERSION}/payments`;
  
  const response = http.post(url, JSON.stringify(paymentData), {
    headers: getHeaders(),
    tags: { name: 'CreatePayment' },
  });
  
  check(response, {
    'status is 201 or 429': (r) => r.status === 201 || r.status === 429, // 429 = rate limited
  });
  
  sleep(0.1); // Minimal sleep during spike
}

