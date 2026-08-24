#!/bin/bash
# KKBOT Local Development Environment Setup
# This script sets up a complete local development environment for KKBOT

set -e

echo "=== KKBOT Local Development Environment ==="
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
KKBOT_VERSION="24.1.0"
DOTNET_VERSION="10.0"
POSTGRES_VERSION="16"
REDIS_VERSION="7"
RABBITMQ_VERSION="3.12"

# Detect OS
if [[ "$OSTYPE" == "linux-gnu"* ]]; then
    OS="linux"
elif [[ "$OSTYPE" == "darwin"* ]]; then
    OS="macos"
elif [[ "$OSTYPE" == "msys" || "$OSTYPE" == "cygwin" ]]; then
    OS="windows"
else
    OS="unknown"
fi

echo -e "${BLUE}Detected OS: ${OS}${NC}"

# Function to check if command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Function to install prerequisites
install_prerequisites() {
    echo -e "${YELLOW}Installing prerequisites...${NC}"
    
    case $OS in
        linux)
            if command_exists apt-get; then
                sudo apt-get update
                sudo apt-get install -y curl wget git docker.io docker-compose postgresql-client redis-tools
            elif command_exists dnf; then
                sudo dnf install -y curl wget git docker docker-compose postgresql redis
            elif command_exists pacman; then
                sudo pacman -S --noconfirm curl wget git docker docker-compose postgresql redis
            fi
            ;;
        macos)
            if ! command_exists brew; then
                echo "Installing Homebrew..."
                /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
            fi
            brew install curl wget git docker docker-compose postgresql redis
            ;;
        windows)
            echo "Please install Docker Desktop, Git, and .NET SDK manually on Windows"
            echo "Visit: https://dotnet.microsoft.com/download/dotnet/10.0"
            ;;
    esac
}

# Function to install .NET SDK
install_dotnet() {
    echo -e "${YELLOW}Installing .NET ${DOTNET_VERSION} SDK...${NC}"
    
    if command_exists dotnet; then
        CURRENT_VERSION=$(dotnet --version | cut -d. -f1)
        if [ "$CURRENT_VERSION" -ge "10" ]; then
            echo -e "${GREEN}.NET ${DOTNET_VERSION} already installed${NC}"
            return
        fi
    fi
    
    case $OS in
        linux)
            wget https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
            chmod +x /tmp/dotnet-install.sh
            /tmp/dotnet-install.sh --version $DOTNET_VERSION --install-dir $HOME/.dotnet
            export PATH="$HOME/.dotnet:$PATH"
            echo 'export PATH="$HOME/.dotnet:$PATH"' >> ~/.bashrc
            ;;
        macos)
            brew install dotnet@10
            ;;
    esac
}

# Function to start infrastructure services
start_infrastructure() {
    echo -e "${YELLOW}Starting infrastructure services...${NC}"
    
    # Create docker-compose.yml for local development
    cat > docker-compose.dev.yml <<'EOF'
version: '3.8'

services:
  postgres:
    image: postgres:${POSTGRES_VERSION}-alpine
    environment:
      POSTGRES_DB: kkbot
      POSTGRES_USER: kkbot
      POSTGRES_PASSWORD: kkbot_dev_password
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U kkbot -d kkbot"]
      interval: 10s
      timeout: 5s
      retries: 5

  redis:
    image: redis:${REDIS_VERSION}-alpine
    command: redis-server --requirepass kkbot_dev_password --maxmemory 256mb --maxmemory-policy allkeys-lru
    ports:
      - "6379:6379"
    volumes:
      - redis_data:/data
    healthcheck:
      test: ["CMD", "redis-cli", "-a", "kkbot_dev_password", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5

  rabbitmq:
    image: rabbitmq:${RABBITMQ_VERSION}-management-alpine
    environment:
      RABBITMQ_DEFAULT_USER: kkbot
      RABBITMQ_DEFAULT_PASS: kkbot_dev_password
    ports:
      - "5672:5672"
      - "15672:15672"
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "check_running"]
      interval: 10s
      timeout: 5s
      retries: 5

  jaeger:
    image: jaegertracing/all-in-one:1.50
    ports:
      - "16686:16686"
      - "6831:6831/udp"
      - "14268:14268"
    environment:
      COLLECTOR_OTLP_ENABLED: true

  prometheus:
    image: prom/prometheus:v2.48.0
    ports:
      - "9090:9090"
    volumes:
      - ./monitoring/prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus_data:/prometheus
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'

  grafana:
    image: grafana/grafana:10.2.0
    ports:
      - "3000:3000"
    environment:
      GF_SECURITY_ADMIN_USER: admin
      GF_SECURITY_ADMIN_PASSWORD: admin
      GF_USERS_ALLOW_SIGN_UP: "false"
    volumes:
      - grafana_data:/var/lib/grafana
      - ./monitoring/grafana/dashboards:/etc/grafana/provisioning/dashboards
      - ./monitoring/grafana/datasources:/etc/grafana/provisioning/datasources

volumes:
  postgres_data:
  redis_data:
  rabbitmq_data:
  prometheus_data:
  grafana_data:
EOF

    # Start services
    docker-compose -f docker-compose.dev.yml up -d
    
    # Wait for services to be healthy
    echo "Waiting for services to be ready..."
    sleep 10
    
    # Verify services
    docker-compose -f docker-compose.dev.yml ps
}

# Function to setup database
setup_database() {
    echo -e "${YELLOW}Setting up database...${NC}"
    
    # Wait for PostgreSQL to be ready
    until docker-compose -f docker-compose.dev.yml exec -T postgres pg_isready -U kkbot -d kkbot >/dev/null 2>&1; do
        echo "Waiting for PostgreSQL..."
        sleep 2
    done
    
    # Run migrations
    dotnet ef database update --project src/KKBOT.Persistence --startup-project src/KKBOT.API
}

# Function to build solution
build_solution() {
    echo -e "${YELLOW}Building solution...${NC}"
    
    dotnet restore KKBOT.sln
    dotnet build KKBOT.sln --configuration Debug --no-restore
}

# Function to run tests
run_tests() {
    echo -e "${YELLOW}Running tests...${NC}"
    
    dotnet test KKBOT.sln --configuration Debug --no-build --verbosity normal
}

# Function to start API
start_api() {
    echo -e "${YELLOW}Starting KKBOT API...${NC}"
    
    cd src/KKBOT.API
    dotnet run --configuration Debug &
    API_PID=$!
    cd ../..
    
    echo "API PID: $API_PID"
    echo $API_PID > .api.pid
}

# Function to start dashboard
start_dashboard() {
    echo -e "${YELLOW}Starting KKBOT Dashboard...${NC}"
    
    cd src/KKBOT.UI.Dashboard
    dotnet run --configuration Debug &
    DASHBOARD_PID=$!
    cd ../..
    
    echo "Dashboard PID: $DASHBOARD_PID"
    echo $DASHBOARD_PID > .dashboard.pid
}

# Function to show status
show_status() {
    echo ""
    echo -e "${GREEN}=== KKBOT Development Environment Ready ===${NC}"
    echo ""
    echo -e "${BLUE}Services:${NC}"
    echo "  PostgreSQL:  localhost:5432 (kkbot/kkbot_dev_password)"
    echo "  Redis:       localhost:6379 (password: kkbot_dev_password)"
    echo "  RabbitMQ:    localhost:5672 (management: localhost:15672)"
    echo "  Jaeger:      localhost:16686"
    echo "  Prometheus:  localhost:9090"
    echo "  Grafana:     localhost:3000 (admin/admin)"
    echo ""
    echo -e "${BLUE}Applications:${NC}"
    echo "  KKBOT API:       http://localhost:5000"
    echo "  Swagger UI:      http://localhost:5000/swagger"
    echo "  Dashboard:       http://localhost:5001"
    echo ""
    echo -e "${BLUE}Useful commands:${NC}"
    echo "  Stop all:        ./dev-env.sh stop"
    echo "  View logs:       ./dev-env.sh logs"
    echo "  Run tests:       ./dev-env.sh test"
    echo "  Rebuild:         ./dev-env.sh build"
    echo ""
}

# Function to stop environment
stop_environment() {
    echo -e "${YELLOW}Stopping KKBOT development environment...${NC}"
    
    # Stop .NET processes
    if [ -f .api.pid ]; then
        kill $(cat .api.pid) 2>/dev/null || true
        rm .api.pid
    fi
    if [ -f .dashboard.pid ]; then
        kill $(cat .dashboard.pid) 2>/dev/null || true
        rm .dashboard.pid
    fi
    
    # Stop Docker services
    docker-compose -f docker-compose.dev.yml down
    
    echo -e "${GREEN}Environment stopped${NC}"
}

# Function to show logs
show_logs() {
    docker-compose -f docker-compose.dev.yml logs -f --tail=100
}

# Main script logic
case "${1:-start}" in
    start)
        install_prerequisites
        install_dotnet
        start_infrastructure
        setup_database
        build_solution
        start_api
        start_dashboard
        show_status
        ;;
    stop)
        stop_environment
        ;;
    restart)
        stop_environment
        sleep 3
        $0 start
        ;;
    build)
        build_solution
        ;;
    test)
        run_tests
        ;;
    logs)
        show_logs
        ;;
    status)
        show_status
        ;;
    *)
        echo "Usage: $0 {start|stop|restart|build|test|logs|status}"
        exit 1
        ;;
esac
